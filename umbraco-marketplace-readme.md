## Umbraco.Forms.Automate

Umbraco Forms triggers and actions for Umbraco Automate - react to form submissions and manage entries from your automations.

### Features

- **2 Triggers** - React to form submission and entry approval events, with optional per-form filtering
- **3 Actions** - Submit form entries programmatically, export entries (with optional approved-only filter and pagination), and delete entries from automation steps
- **Rich Trigger Outputs** - Form ID, form name, record ID, state, timestamps, IP, member key, culture, field values as JSON, plus the submitted form's fields exposed individually (bindable as `fields.<alias>` in the expression picker)
- **Zero Configuration** - Triggers and actions are automatically discovered by Umbraco Automate

Example: post to a channel when a form is submitted, export entries on a schedule, or delete spam entries as they arrive.

### Requirements

- Umbraco CMS 17.x
- Umbraco Forms 17.3+
- Umbraco.Automate 17.0+
- .NET 10.0
