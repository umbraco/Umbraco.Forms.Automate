using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Bindings;
using Umbraco.Forms.Core.Data.Storage;
using Umbraco.Forms.Core.Enums;
using Umbraco.Forms.Core.Security;
using Umbraco.Forms.Core.Services;

namespace Umbraco.Forms.Automate.Actions;

/// <summary>
/// Deletes a single form entry.
/// </summary>
/// <remarks>
/// Automate runs actions as the workspace's service account, so the Forms permission checks below
/// apply to that account in the same way the backoffice applies them to a signed-in user: it needs
/// the "Delete entries" permission and access to the form.
/// </remarks>
[Action("umbracoForms.deleteEntry", "Delete Form Entry",
    Description = "Permanently deletes a form entry.",
    Group = "Forms",
    Icon = "icon-trash",
    RequiredSections = [Constants.Sections.Forms])]
public sealed class DeleteEntryAction : ActionBase<DeleteEntrySettings, DeleteEntryOutput>
{
    private readonly IFormService _formService;
    private readonly IRecordStorage _recordStorage;
    private readonly IRecordService _recordService;
    private readonly IFormsSecurity _formsSecurity;
    private readonly BindingEvaluator _bindingEvaluator;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteEntryAction"/> class.
    /// </summary>
    public DeleteEntryAction(
        ActionInfrastructure infrastructure,
        IFormService formService,
        IRecordStorage recordStorage,
        IRecordService recordService,
        IFormsSecurity formsSecurity,
        BindingEvaluator bindingEvaluator)
        : base(infrastructure)
    {
        _formService = formService;
        _recordStorage = recordStorage;
        _recordService = recordService;
        _formsSecurity = formsSecurity;
        _bindingEvaluator = bindingEvaluator;
    }

    /// <inheritdoc />
    public override async Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
    {
        var settings = context.GetSettings<DeleteEntrySettings>();

        // Blank settings mean "the entry that started the run". They are resolved here rather than
        // through default values on the settings, because the flow editor doesn't save defaults.
        var formIdValue = OrTriggerValue(settings.FormId, "${ trigger.formId }", context);
        if (string.IsNullOrWhiteSpace(formIdValue) || !Guid.TryParse(formIdValue, out var formId))
        {
            return ActionResult.Failed(
                new ArgumentException("A valid form is required. Leave it blank only when the automation starts from a form trigger."),
                StepRunErrorCategory.Validation);
        }

        var recordUniqueIdValue = OrTriggerValue(settings.RecordUniqueId, "${ trigger.recordUniqueId }", context);
        if (string.IsNullOrWhiteSpace(recordUniqueIdValue) || !Guid.TryParse(recordUniqueIdValue, out var recordUniqueId))
        {
            return ActionResult.Failed(
                new ArgumentException("A valid Entry ID is required. Leave it blank only when the automation starts from a form trigger."),
                StepRunErrorCategory.Validation);
        }

        // Check permissions before looking anything up, so an unauthorised account can't use the
        // step to probe which forms or entries exist.
        if (!_formsSecurity.CanCurrentUserDeleteEntries())
        {
            return ActionResult.Failed(
                new UnauthorizedAccessException("The automation's service account does not have the Forms \"Delete entries\" permission."),
                StepRunErrorCategory.Authentication);
        }

        if (!_formsSecurity.HasAccessToForm(formId))
        {
            return ActionResult.Failed(
                new UnauthorizedAccessException($"The automation's service account does not have access to form '{formId}'."),
                StepRunErrorCategory.Authentication);
        }

        var form = _formService.Get(formId);
        if (form is null)
        {
            return ActionResult.Failed(
                new InvalidOperationException($"Form '{formId}' not found."),
                StepRunErrorCategory.Validation);
        }

        var deleted = false;
        var record = _recordStorage.GetRecordByUniqueId(recordUniqueId, form);
        if (record is not null)
        {
            // Delete through the record service rather than storage so the form's "on delete"
            // workflows run and the deletion is logged against the service account. Those
            // workflows can change the record's state to cancel the delete, so read it back.
            await _recordService.DeleteAsync(record, form);
            deleted = record.State == FormState.Deleted;
        }

        return Success(new DeleteEntryOutput
        {
            FormId = formId,
            RecordUniqueId = recordUniqueId,
            Deleted = deleted,
        });
    }

    private string? OrTriggerValue(string? value, string triggerBinding, ActionContext context)
        => string.IsNullOrWhiteSpace(value) && context.BindingData is not null
            ? _bindingEvaluator.Evaluate(triggerBinding, context.BindingData)
            : value;
}
