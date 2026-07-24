using Json.Schema;
using Umbraco.Automate.Core.Triggers;
using Umbraco.Cms.Core.Notifications;

namespace Umbraco.Forms.Automate.Triggers;

/// <summary>
/// Shared base for form record triggers (submitted, approved) whose output schema is
/// resolved dynamically from the picked form(s), exposing their fields to the
/// "Insert Binding Expression" picker.
/// </summary>
/// <typeparam name="TNotification">The Umbraco Forms notification type.</typeparam>
public abstract class DynamicFormRecordTriggerBase<TNotification>
    : DynamicOutputNotificationTriggerBase<FormRecordTriggerSettings, TNotification>
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
    protected override bool CanHandle(object output, FormRecordTriggerSettings? settings)
    {
        var formIds = FormFieldResolver.ParseFormIds(settings?.FormIds);
        if (formIds.Count == 0)
        {
            return true;
        }

        return output is FormRecordOutput record && formIds.Contains(record.FormId);
    }
}
