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
}
