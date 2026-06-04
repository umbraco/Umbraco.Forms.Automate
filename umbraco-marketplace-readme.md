## Umbraco.Forms.Automate

Umbraco Forms triggers and actions for Umbraco Automate - react to form submissions and manage entries from your automations.

### Features

- **2 Triggers** - React to form submission and entry approval events, with optional per-form filtering
- **2 Actions** - Submit form entries programmatically and export entries (with optional approved-only filter and pagination) from automation steps
- **Rich Trigger Outputs** - Form ID, form name, record ID, state, timestamps, IP, member key, culture, and field values as JSON
- **Zero Configuration** - Triggers and actions are automatically discovered by Umbraco Automate

Example: post to a channel when a form is submitted, or export entries on a schedule.

### Requirements

- Umbraco CMS 17.x
- Umbraco Forms 17.3+
- Umbraco.Automate 17.0+
- .NET 10.0
