using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Runs;
using Umbraco.Cms.Core.Configuration.Models;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Services;
using Umbraco.Extensions;
using Umbraco.Forms.Core.Data.Storage;
using Umbraco.Forms.Core.Enums;
using Umbraco.Forms.Core.Models;
using Umbraco.Forms.Core.Persistence.Dtos;
using Umbraco.Forms.Core.Searchers;
using Umbraco.Forms.Core.Services;

namespace Umbraco.Forms.Automate.Actions;

/// <summary>
/// Exports form entries as a JSON array.
/// </summary>
[Action("umbracoForms.exportEntries", "Export Form Entries",
    Description = "Exports form entries as JSON.",
    Group = "Forms",
    Icon = "icon-list",
    RequiredSections = [Constants.Sections.Forms])]
public sealed class ExportEntriesAction : ActionBase<ExportEntriesSettings, ExportEntriesOutput>
{
    // The earliest Created date to query from. The searcher substitutes "the last 2000 days" for the default
    // DateTime, which would silently drop older entries, so an explicit floor is passed instead.
    private static readonly DateTime EarliestCreated = new(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    // The CMS only ships "content/isSensitiveValue" in a few server-side language files (de-CH, fr-CH, it-CH), so
    // the lookup misses for most cultures and the text service returns the key in brackets. This is used then.
    private const string DefaultWithheldText = "This value is hidden.";

    private readonly IFormService _formService;
    private readonly IFormRecordSearcher _formRecordSearcher;
    private readonly IRecordStorage _recordStorage;
    private readonly IBackOfficeSecurityAccessor _backOfficeSecurityAccessor;
    private readonly ILocalizedTextService _localizedTextService;
    private readonly GlobalSettings _globalSettings;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExportEntriesAction"/> class.
    /// </summary>
    /// <param name="infrastructure">The action infrastructure.</param>
    /// <param name="formService">The form service.</param>
    /// <param name="formRecordSearcher">The searcher used to page and filter entries in the database.</param>
    /// <param name="recordStorage">The record storage the entries are read from.</param>
    /// <param name="backOfficeSecurityAccessor">Used to find the current user, who decides whether sensitive values are exported.</param>
    /// <param name="localizedTextService">Provides the text that replaces a withheld sensitive value.</param>
    /// <param name="globalSettings">Provides the default backoffice language, used when the user has none.</param>
    /// <param name="recordReaderService">
    /// Not used. It is only here so this constructor's parameters include the obsolete constructor's: the DI container
    /// throws an ambiguity error when a shorter constructor has a parameter the longest one lacks. It is optional
    /// because Forms only registers it when record indexing is enabled. Removed with the obsolete constructor in v21.0.0.
    /// </param>
    [ActivatorUtilitiesConstructor]
    public ExportEntriesAction(
        ActionInfrastructure infrastructure,
        IFormService formService,
        IFormRecordSearcher formRecordSearcher,
        IRecordStorage recordStorage,
        IBackOfficeSecurityAccessor backOfficeSecurityAccessor,
        ILocalizedTextService localizedTextService,
        IOptions<GlobalSettings> globalSettings,
        IRecordReaderService? recordReaderService = null)
        : base(infrastructure)
    {
        _formService = formService;
        _formRecordSearcher = formRecordSearcher;
        _recordStorage = recordStorage;
        _backOfficeSecurityAccessor = backOfficeSecurityAccessor;
        _localizedTextService = localizedTextService;
        _globalSettings = globalSettings.Value;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ExportEntriesAction"/> class.
    /// </summary>
    /// <param name="infrastructure">The action infrastructure.</param>
    /// <param name="recordReaderService">No longer used: entries are read from the database, not the search index.</param>
    [Obsolete("Use the constructor taking IFormService, IFormRecordSearcher, IRecordStorage, IBackOfficeSecurityAccessor, ILocalizedTextService and IOptions<GlobalSettings>. Will be removed in v21.0.0.")]
    public ExportEntriesAction(
        ActionInfrastructure infrastructure,
        IRecordReaderService recordReaderService)
        : this(
            infrastructure,
            StaticServiceProvider.Instance.GetRequiredService<IFormService>(),
            StaticServiceProvider.Instance.GetRequiredService<IFormRecordSearcher>(),
            StaticServiceProvider.Instance.GetRequiredService<IRecordStorage>(),
            StaticServiceProvider.Instance.GetRequiredService<IBackOfficeSecurityAccessor>(),
            StaticServiceProvider.Instance.GetRequiredService<ILocalizedTextService>(),
            StaticServiceProvider.Instance.GetRequiredService<IOptions<GlobalSettings>>(),
            recordReaderService)
    {
    }

    /// <inheritdoc />
    public override Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
    {
        var settings = context.GetSettings<ExportEntriesSettings>();

        if (string.IsNullOrWhiteSpace(settings.FormId) || !Guid.TryParse(settings.FormId, out var formId))
        {
            return Task.FromResult(ActionResult.Failed(
                new ArgumentException("A valid Form ID is required."),
                StepRunErrorCategory.Validation));
        }

        var pageSize = Math.Clamp(settings.PageSize > 0 ? settings.PageSize : 100, 1, 500);

        var (total, form, records) = ReadRecords(formId, pageSize, settings.ApprovedOnly);

        // Same rule as the backoffice export: sensitive values are only exported for a user with sensitive-data
        // access, and no user counts as no access. Otherwise they are replaced with the same "sensitive" text.
        var withholdSensitive = form is not null && !HasAccessToSensitiveData();
        var sensitiveFieldKeys = withholdSensitive
            ? form!.AllFields.Where(f => f.ContainsSensitiveData).Select(f => f.Id.ToString()).ToArray()
            : [];
        var withheldText = withholdSensitive && sensitiveFieldKeys.Length > 0
            ? GetWithheldText()
            : string.Empty;

        // Build a JSON array directly, embedding the pre-serialized field data
        // from GenerateRecordDataAsJson() without a deserialize-then-reserialize cycle.
        var entriesArray = new JsonArray();
        foreach (var r in records)
        {
            var fields = JsonNode.Parse(r.GenerateRecordDataAsJson())!.AsObject();
            foreach (var key in sensitiveFieldKeys.Where(fields.ContainsKey))
            {
                fields[key] = withheldText;
            }

            var entry = new JsonObject
            {
                ["uniqueId"] = r.UniqueId.ToString(),
                ["created"] = r.Created,
                ["updated"] = r.Updated,
                ["state"] = r.State.ToString(),
                ["ip"] = r.IP,
                // The member is sensitive data too: the backoffice export leaves it out for a user without access.
                ["memberKey"] = withholdSensitive ? null : r.MemberKey,
                ["culture"] = r.Culture,
                ["fields"] = fields,
            };
            entriesArray.Add(entry);
        }

        return Task.FromResult<ActionResult>(Success(new ExportEntriesOutput
        {
            FormId = formId,
            TotalCount = total,
            ExportedCount = entriesArray.Count,
            EntriesJson = entriesArray.ToJsonString(),
        }));
    }

    // Localized in the current backoffice user's language, else the default backoffice language, not
    // CultureInfo.CurrentCulture: a background run's thread culture is unrelated to any language file.
    private string GetWithheldText()
    {
        var language = _backOfficeSecurityAccessor.BackOfficeSecurity?.CurrentUser?.Language;
        if (string.IsNullOrWhiteSpace(language))
        {
            language = _globalSettings.DefaultUILanguage;
        }

        CultureInfo culture;
        try
        {
            culture = UmbracoUserExtensions.GetUserCulture(language, _localizedTextService, _globalSettings);
        }
        catch (CultureNotFoundException)
        {
            culture = CultureInfo.GetCultureInfo("en-US");
        }

        // A missing key comes back as "[key]", which must never end up in the export.
        var text = _localizedTextService.Localize("content", "isSensitiveValue", culture).Trim();
        return text.Length == 0 || text == "[isSensitiveValue]" ? DefaultWithheldText : text;
    }

    private bool HasAccessToSensitiveData()
        => _backOfficeSecurityAccessor.BackOfficeSecurity?.CurrentUser?.HasAccessToSensitiveData() == true;

    // Reads from the database rather than IRecordReaderService, which is backed by the search index and can
    // lag behind a record submitted moments earlier. The searcher pages, filters and sorts in SQL and returns
    // display models, so only the matching ids are taken from it and the Records are re-read from storage:
    // that keeps GenerateRecordDataAsJson() as the source of the field data.
    private (long Total, Form? Form, IReadOnlyList<Record> Records) ReadRecords(Guid formId, int pageSize, bool approvedOnly)
    {
        var form = _formService.Get(formId);
        if (form is null)
        {
            return (0, null, []);
        }

        var filter = new RecordFilter
        {
            Skip = 0,
            Take = pageSize,
            SortBy = "created",
            SortOrder = RecordSorting.Descending,
            StartDate = EarliestCreated,
            States = approvedOnly ? [FormState.Approved] : [],
        };

        var page = _formRecordSearcher.QueryDataBase(formId, filter, form);
        var uniqueIds = page.Results.Select(x => x.UniqueId).ToArray();
        if (uniqueIds.Length == 0)
        {
            return (page.TotalNumberOfResults, form, []);
        }

        // Storage does not preserve the requested order, so project back onto the searcher's order.
        var recordsById = _recordStorage.GetRecords(uniqueIds, form)
            .GroupBy(r => r.UniqueId)
            .ToDictionary(g => g.Key, g => g.First());

        return (
            page.TotalNumberOfResults,
            form,
            uniqueIds.Where(recordsById.ContainsKey).Select(id => recordsById[id]).ToList());
    }
}
