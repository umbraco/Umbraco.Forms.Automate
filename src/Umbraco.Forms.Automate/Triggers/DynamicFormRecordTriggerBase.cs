using Json.Schema;
using Umbraco.Automate.Core.Triggers;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Forms.Core.Models;
using Umbraco.Forms.Core.Persistence.Dtos;

namespace Umbraco.Forms.Automate.Triggers;

/// <summary>
/// Shared base for form record triggers (submitted, approved) whose output schema is
/// resolved dynamically from the picked form(s), exposing their fields to the
/// "Insert Binding Expression" picker.
/// </summary>
/// <remarks>
/// Declares <see cref="FormRecordOutput"/> as the static output type rather than deriving from
/// <c>DynamicOutputNotificationTriggerBase</c>, which pins the output type to <c>object</c>. The
/// dispatcher only applies a trigger's settings filter when it can deserialize the event payload
/// into the trigger's declared output type, so an <c>object</c> output silently disables
/// <see cref="CanHandle"/>. The dynamic schema is still provided by overriding
/// <see cref="HasDynamicOutputSchema"/> and <see cref="GetOutputSchemaAsync"/>.
/// </remarks>
/// <typeparam name="TNotification">The Umbraco Forms notification type.</typeparam>
public abstract class DynamicFormRecordTriggerBase<TNotification>
    : NotificationTriggerBase<FormRecordTriggerSettings, FormRecordOutput, TNotification>
    where TNotification : INotification
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DynamicFormRecordTriggerBase{TNotification}"/> class.
    /// </summary>
    protected DynamicFormRecordTriggerBase(TriggerInfrastructure infrastructure, FormFieldResolver resolver)
        : base(infrastructure) => Resolver = resolver;

    /// <summary>
    /// Gets the resolver used to build the dynamic output schema and runtime field payload.
    /// </summary>
    protected FormFieldResolver Resolver { get; }

    /// <summary>
    /// Builds the trigger event for a form record, omitting every field the form marks as
    /// containing sensitive data.
    /// </summary>
    /// <remarks>
    /// Every form record trigger must build its output through this method. The payload is
    /// serialized into the outbox row before any automation, workspace or service account is
    /// resolved, so this is the only point at which sensitive values can be kept out of
    /// Automate's storage entirely — a later filter would already be too late.
    /// </remarks>
    /// <param name="record">The record the event is for.</param>
    /// <param name="form">The form the record belongs to.</param>
    /// <returns>The trigger event to dispatch.</returns>
    protected TriggerEvent CreateEvent(Record record, Form form)
        => new TriggerEvent<FormRecordOutput>
        {
            TriggerAlias = Alias,
            InitiatorType = "system",
            IdempotencyKey = GenerateIdempotencyKey(record.UniqueId, record.Id),
            Output = new FormRecordOutput
            {
                FormId = form.Id,
                FormName = form.Name,
                RecordUniqueId = record.UniqueId,
                State = record.State.ToString(),
                Created = record.Created,
                Ip = record.IP,
                MemberKey = record.MemberKey,
                Culture = record.Culture,
                RecordFieldsJson = FormFieldResolver.GenerateRecordFieldsJson(record, form),
                Fields = FormFieldResolver.ExtractFields(record, form),
            },
        };

    /// <inheritdoc />
    public override bool HasDynamicOutputSchema => true;

    /// <inheritdoc />
    protected override Task<JsonSchema?> GetOutputSchemaAsync(
        FormRecordTriggerSettings? settings,
        CancellationToken cancellationToken)
        => Task.FromResult<JsonSchema?>(Resolver.BuildOutputSchema(settings?.FormIds));

    /// <summary>
    /// Filters events for a subscribing automation by its configured form selection.
    /// An empty selection (the default) matches every form; otherwise only records
    /// belonging to one of the picked forms fire the automation.
    /// </summary>
    /// <param name="output">The record output produced by <c>MapEvent</c>.</param>
    /// <param name="settings">The automation's resolved trigger settings, or <c>null</c> if unconfigured.</param>
    /// <returns><c>true</c> if the event should fire for an automation with these settings.</returns>
    protected override bool CanHandle(FormRecordOutput output, FormRecordTriggerSettings? settings)
    {
        var formIds = settings?.FormIds;

        return formIds is not { Count: > 0 } || formIds.Contains(output.FormId);
    }
}
