## Umbraco.Forms.Automate

Umbraco Forms triggers and actions for Umbraco Automate - react to form submissions and manage entries from your automations.

### Features

- **2 Triggers** - React to form submission and entry approval events, with optional per-form filtering
- **2 Actions** - Submit form entries programmatically and export entries (with optional approved-only filter and pagination) from automation steps
- **Rich Trigger Outputs** - Form ID, form name, record ID, state, timestamps, IP, member key, culture, field values as JSON, plus the submitted form's fields exposed individually (bindable as `fields.<alias>` in the expression picker)
- **Zero Configuration** - Triggers and actions are automatically discovered by Umbraco Automate

Example: post to a channel when a form is submitted, or export entries on a schedule.

### Requirements

- .NET 11.0
- Umbraco CMS 19
- Umbraco Forms 19
- Umbraco Automate 19
