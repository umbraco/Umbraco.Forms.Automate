using System.Globalization;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Runs;
using Umbraco.Automate.Core.Settings;
using Umbraco.Automate.Testing;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Services;
using Umbraco.Forms.Automate.Actions;
using Umbraco.Forms.Core.Data.Storage;
using Umbraco.Forms.Core.Enums;
using Umbraco.Forms.Core.Models;
using Umbraco.Forms.Core.Persistence.Dtos;
using Umbraco.Forms.Core.Searchers;
using Umbraco.Forms.Core.Services;
using Record = Umbraco.Forms.Core.Persistence.Dtos.Record;

namespace Umbraco.Forms.Automate.Tests.Unit.Actions;

public class ExportEntriesActionTests
{
    private readonly Mock<IFormService> _formService = new();
    private readonly Mock<IFormRecordSearcher> _searcher = new();
    private readonly Mock<IRecordStorage> _recordStorage = new();
    private readonly Mock<IRecordReaderService> _readerService = new();
    private readonly Mock<IBackOfficeSecurityAccessor> _securityAccessor = new();
    private readonly Mock<IBackOfficeSecurity> _security = new();
    private readonly Mock<ILocalizedTextService> _localizedText = new();

    private readonly Guid _formId = Guid.NewGuid();

    public ExportEntriesActionTests()
    {
        _formService.Setup(s => s.Get(_formId)).Returns(new Form { Id = _formId });
        _securityAccessor.SetupGet(a => a.BackOfficeSecurity).Returns(_security.Object);
        _localizedText
            .Setup(t => t.Localize("content", "isSensitiveValue", It.IsAny<CultureInfo>(), It.IsAny<IDictionary<string, string?>?>()))
            .Returns("SENSITIVE");
    }

    [Fact]
    public async Task InvalidFormId_ReturnsValidationError()
    {
        var result = await Execute(new ExportEntriesSettings { FormId = "not-a-guid" });

        result.Status.ShouldBe(ActionResultStatus.Failed);
        result.ErrorCategory.ShouldBe(StepRunErrorCategory.Validation);
    }

    [Fact]
    public async Task EmptyResults_ReturnsEmptyJson()
    {
        SetupSearch(total: 0);

        var result = await Execute(new ExportEntriesSettings { FormId = _formId.ToString() });

        result.Status.ShouldBe(ActionResultStatus.Success);

        var output = result.OutputData.ShouldBeOfType<ExportEntriesOutput>();
        output.FormId.ShouldBe(_formId);
        output.TotalCount.ShouldBe(0);
        output.ExportedCount.ShouldBe(0);
        output.EntriesJson.ShouldBe("[]");
        _recordStorage.Verify(s => s.GetRecords(It.IsAny<IEnumerable<Guid>>(), It.IsAny<Form>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public async Task UnknownForm_ReturnsEmptyJson()
    {
        var unknown = Guid.NewGuid();

        var result = await Execute(new ExportEntriesSettings { FormId = unknown.ToString() });

        result.Status.ShouldBe(ActionResultStatus.Success);
        var output = result.OutputData.ShouldBeOfType<ExportEntriesOutput>();
        output.TotalCount.ShouldBe(0);
        output.EntriesJson.ShouldBe("[]");
        _searcher.Verify(s => s.QueryDataBase(It.IsAny<Guid>(), It.IsAny<RecordFilter>(), It.IsAny<Form>()), Times.Never);
    }

    [Fact]
    public async Task WithRecords_ReturnsJsonArray()
    {
        var record = NewRecord(new DateTime(2026, 3, 27, 12, 0, 0, DateTimeKind.Utc));
        SetupSearch(total: 1, record);

        var result = await Execute(new ExportEntriesSettings { FormId = _formId.ToString() });

        result.Status.ShouldBe(ActionResultStatus.Success);

        var output = result.OutputData.ShouldBeOfType<ExportEntriesOutput>();
        output.TotalCount.ShouldBe(1);
        output.ExportedCount.ShouldBe(1);
        var json = output.EntriesJson.ShouldNotBeNull();
        json.ShouldContain(record.UniqueId.ToString());
        json.ShouldContain("\"ip\":\"10.0.0.1\"");
        json.ShouldContain("\"state\":\"Submitted\"");
        json.ShouldContain("\"fields\":{}");
    }

    [Fact]
    public async Task ReadsFromDatabase_NotFromTheSearchIndex()
    {
        SetupSearch(total: 1, NewRecord(DateTime.UtcNow));

        await Execute(new ExportEntriesSettings { FormId = _formId.ToString() });

        _searcher.Verify(s => s.QueryDataBase(_formId, It.IsAny<RecordFilter>(), It.IsAny<Form>()), Times.Once);
        _readerService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Records_KeepSearcherOrder_NewestFirst()
    {
        var newest = NewRecord(new DateTime(2026, 3, 3, 0, 0, 0, DateTimeKind.Utc));
        var middle = NewRecord(new DateTime(2026, 3, 2, 0, 0, 0, DateTimeKind.Utc));
        var oldest = NewRecord(new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc));
        _searcher.Setup(s => s.QueryDataBase(_formId, It.IsAny<RecordFilter>(), It.IsAny<Form>()))
            .Returns(Collection(3, newest, middle, oldest));

        // Storage hands the records back in a different order to the one requested.
        _recordStorage.Setup(s => s.GetRecords(It.IsAny<IEnumerable<Guid>>(), It.IsAny<Form>(), true))
            .Returns([oldest, newest, middle]);

        var result = await Execute(new ExportEntriesSettings { FormId = _formId.ToString() });

        var json = result.OutputData.ShouldBeOfType<ExportEntriesOutput>().EntriesJson.ShouldNotBeNull();
        json.IndexOf(newest.UniqueId.ToString(), StringComparison.Ordinal)
            .ShouldBeLessThan(json.IndexOf(middle.UniqueId.ToString(), StringComparison.Ordinal));
        json.IndexOf(middle.UniqueId.ToString(), StringComparison.Ordinal)
            .ShouldBeLessThan(json.IndexOf(oldest.UniqueId.ToString(), StringComparison.Ordinal));
    }

    [Fact]
    public async Task Default_QueriesAllStates_NewestFirst_FirstPageOf100_FromEarliestDate()
    {
        RecordFilter? captured = null;
        SetupSearch(total: 0, onQuery: f => captured = f);

        await Execute(new ExportEntriesSettings { FormId = _formId.ToString() });

        captured.ShouldNotBeNull();
        captured.States.ShouldBeEmpty();
        captured.Skip.ShouldBe(0);
        captured.Take.ShouldBe(100);
        captured.SortBy.ShouldBe("created");
        captured.SortOrder.ShouldBe(RecordSorting.Descending);
        captured.StartDate.ShouldBeLessThan(new DateTime(1970, 1, 1));
    }

    [Fact]
    public async Task ApprovedOnly_FiltersToApprovedState()
    {
        RecordFilter? captured = null;
        SetupSearch(total: 0, onQuery: f => captured = f);

        var result = await Execute(new ExportEntriesSettings { FormId = _formId.ToString(), ApprovedOnly = true });

        result.Status.ShouldBe(ActionResultStatus.Success);
        captured.ShouldNotBeNull();
        captured.States.ShouldBe([FormState.Approved]);
    }

    [Theory]
    [InlineData(9999, 500)]
    [InlineData(250, 250)]
    [InlineData(0, 100)]
    [InlineData(-5, 100)]
    public async Task PageSize_IsClamped(int requested, int expectedTake)
    {
        RecordFilter? captured = null;
        SetupSearch(total: 0, onQuery: f => captured = f);

        await Execute(new ExportEntriesSettings { FormId = _formId.ToString(), PageSize = requested });

        captured.ShouldNotBeNull();
        captured.Take.ShouldBe(expectedTake);
    }

    [Fact]
    public async Task SensitiveFieldValues_AreWithheld_WhenThereIsNoCurrentUser()
    {
        _security.SetupGet(s => s.CurrentUser).Returns((IUser?)null);

        var json = await ExportRecordWithSensitiveField();

        json.ShouldNotContain("hunter2");
        FieldValue(json, _sensitiveFieldId).ShouldBe("SENSITIVE");
        FieldValue(json, _plainFieldId).ShouldBe("visible");
    }

    [Fact]
    public async Task SensitiveFieldValues_AreWithheld_WhenUserLacksSensitiveDataAccess()
    {
        _security.SetupGet(s => s.CurrentUser).Returns(User(inSensitiveDataGroup: false));

        var json = await ExportRecordWithSensitiveField();

        json.ShouldNotContain("hunter2");
        FieldValue(json, _sensitiveFieldId).ShouldBe("SENSITIVE");
    }

    [Fact]
    public async Task SensitiveFieldValues_AreExported_WhenUserHasSensitiveDataAccess()
    {
        _security.SetupGet(s => s.CurrentUser).Returns(User(inSensitiveDataGroup: true));

        var json = await ExportRecordWithSensitiveField();

        FieldValue(json, _sensitiveFieldId).ShouldBe("hunter2");
        FieldValue(json, _plainFieldId).ShouldBe("visible");
    }

    [Fact]
    public async Task MemberKey_IsWithheld_WithoutSensitiveDataAccess_AndExported_WithIt()
    {
        _security.SetupGet(s => s.CurrentUser).Returns(User(inSensitiveDataGroup: false));
        (await ExportRecordWithSensitiveField()).ShouldContain("\"memberKey\":null");

        _security.SetupGet(s => s.CurrentUser).Returns(User(inSensitiveDataGroup: true));
        (await ExportRecordWithSensitiveField()).ShouldContain("\"memberKey\":\"member-1\"");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ServiceDescriptorRegistration_ResolvesWithMicrosoftDI(bool recordIndexingEnabled)
    {
        // Automate registers actions as ServiceDescriptor(type, type) and resolves them with the container,
        // which throws when two public constructors are ambiguous, unlike ActivatorUtilities. Forms only
        // registers IRecordReaderService when record indexing is enabled.
        var services = new ServiceCollection();
        services.AddSingleton(new ActionInfrastructure(Mock.Of<IEditableModelResolver>()));
        services.AddSingleton(_formService.Object);
        services.AddSingleton(_searcher.Object);
        services.AddSingleton(_recordStorage.Object);
        if (recordIndexingEnabled)
        {
            services.AddSingleton(_readerService.Object);
        }

        services.AddSingleton(_securityAccessor.Object);
        services.AddSingleton(_localizedText.Object);
        services.Add(new ServiceDescriptor(typeof(ExportEntriesAction), typeof(ExportEntriesAction), ServiceLifetime.Transient));

        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<ExportEntriesAction>().ShouldNotBeNull();
    }

    [Fact]
    public async Task ActivatorUtilities_ResolvesWithoutRecordReaderService()
    {
        SetupSearch(total: 0);

        var result = await Execute(new ExportEntriesSettings { FormId = _formId.ToString() }, withRecordReader: false);

        result.Status.ShouldBe(ActionResultStatus.Success);
    }

    private readonly Guid _sensitiveFieldId = Guid.NewGuid();
    private readonly Guid _plainFieldId = Guid.NewGuid();

    private async Task<string> ExportRecordWithSensitiveField()
    {
        _formService.Setup(s => s.Get(_formId)).Returns(new Form
        {
            Id = _formId,
            Pages =
            [
                new Page
                {
                    FieldSets =
                    [
                        new FieldSet
                        {
                            Containers =
                            [
                                new FieldsetContainer
                                {
                                    Fields =
                                    [
                                        new Field { Id = _sensitiveFieldId, Alias = "secret", ContainsSensitiveData = true },
                                        new Field { Id = _plainFieldId, Alias = "plain" },
                                    ],
                                },
                            ],
                        },
                    ],
                },
            ],
        });

        var record = NewRecord(DateTime.UtcNow);
        record.MemberKey = "member-1";
        AddValue(record, _sensitiveFieldId, "secret", "hunter2");
        AddValue(record, _plainFieldId, "plain", "visible");
        SetupSearch(total: 1, record);

        var result = await Execute(new ExportEntriesSettings { FormId = _formId.ToString() });

        return result.OutputData.ShouldBeOfType<ExportEntriesOutput>().EntriesJson.ShouldNotBeNull();
    }

    private static void AddValue(Record record, Guid fieldId, string alias, string value)
    {
        var recordField = new RecordField(new Field { Id = fieldId, Alias = alias });
        recordField.Values.Add(value);
        record.RecordFields.Add(fieldId, recordField);
    }

    private static string? FieldValue(string json, Guid fieldId)
        => JsonNode.Parse(json)![0]!["fields"]![fieldId.ToString()]?.GetValue<string>();

    private static IUser User(bool inSensitiveDataGroup)
    {
        var group = new Mock<IReadOnlyUserGroup>();
        group.SetupGet(g => g.Key).Returns(inSensitiveDataGroup ? Umbraco.Cms.Core.Constants.Security.SensitiveDataGroupKey : Guid.NewGuid());
        var user = new Mock<IUser>();
        user.SetupGet(u => u.Groups).Returns([group.Object]);
        return user.Object;
    }

    private Task<ActionResult> Execute(ExportEntriesSettings settings, bool withRecordReader = true)
    {
        var harness = ActionTestHarness.For<ExportEntriesAction>()
            .WithService(_formService.Object)
            .WithService(_searcher.Object)
            .WithService(_recordStorage.Object)
            .WithService(_securityAccessor.Object)
            .WithService(_localizedText.Object);

        if (withRecordReader)
        {
            harness = harness.WithService(_readerService.Object);
        }

        return harness.WithSettings(settings).ExecuteAsync();
    }

    private static Record NewRecord(DateTime created) => new()
    {
        UniqueId = Guid.NewGuid(),
        State = FormState.Submitted,
        Created = created,
        IP = "10.0.0.1",
    };

    private static EntrySearchResultCollection Collection(long total, params Record[] records) => new()
    {
        TotalNumberOfResults = total,
        Results = records.Select(r => new EntrySearchResult { UniqueId = r.UniqueId }).ToList(),
    };

    private void SetupSearch(long total, Record? first = null, Action<RecordFilter>? onQuery = null)
    {
        var records = first is null ? [] : new[] { first };
        _searcher.Setup(s => s.QueryDataBase(_formId, It.IsAny<RecordFilter>(), It.IsAny<Form>()))
            .Callback<Guid, RecordFilter, Form>((_, f, _) => onQuery?.Invoke(f))
            .Returns(Collection(total, records));
        _recordStorage.Setup(s => s.GetRecords(It.IsAny<IEnumerable<Guid>>(), It.IsAny<Form>(), true))
            .Returns(records.ToList());
    }
}
