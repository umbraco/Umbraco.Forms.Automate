using Umbraco.Automate.Core.Triggers;
using Umbraco.Forms.Core.Services.Notifications;

namespace Umbraco.Forms.Automate.Triggers;

/// <summary>
/// Fires when a form entry is submitted in Umbraco Forms.
/// Produces one <see cref="TriggerEvent"/> per submitted record.
/// </summary>
[Trigger("umbracoForms.formSubmitted", "Form Submitted",
    Description = "Fires when a form entry is submitted.",
    Group = "Forms",
    Icon = "icon-checkbox",
    RequiredSections = [Constants.Sections.Forms])]
public sealed class FormSubmittedTrigger
    : DynamicFormRecordTriggerBase<RecordSubmittedNotification>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FormSubmittedTrigger"/> class.
    /// </summary>
    public FormSubmittedTrigger(TriggerInfrastructure infrastructure, FormFieldResolver resolver)
        : base(infrastructure, resolver)
    {
    }

    /// <inheritdoc />
    public override IEnumerable<TriggerEvent> MapEvent(RecordSubmittedNotification notification)
    {
        yield return CreateEvent(notification.Record, notification.Form);
    }
}
