# Umbraco.Forms.Automate

Umbraco Forms triggers and actions for [Umbraco Automate](https://umbraco.com/products/umbraco-automate/).

## Overview

Umbraco.Forms.Automate is a provider package that connects [Umbraco Forms](https://umbraco.com/products/umbraco-forms/) to Umbraco Automate, exposing form submission and record management events as first-class triggers and actions in Automate flows — for example, posting to a channel when a form is submitted, or exporting entries on a schedule.

## Key Features

- **2 triggers** — react to form submission and entry approval events, with optional per-form filtering
- **2 actions** — submit form entries programmatically and export entries from automation steps
- **Rich trigger outputs** — form ID, form name, record ID, state, timestamps, IP, member key, culture, and field values as JSON
- **Zero configuration** — triggers and actions are automatically discovered by Umbraco Automate

## Installation

```bash
dotnet add package Umbraco.Forms.Automate
```

No further wiring is required — triggers and actions are auto-discovered by Umbraco Automate.

## Requirements

- .NET 10.0
- Umbraco CMS 17.x
- Umbraco Forms 17.3+
- Umbraco.Automate 17.0+

## Triggers

Fire an Automate flow when something happens in Forms.

| Trigger | Fires when… |
|---|---|
| Form Submitted | A form entry is submitted |
| Form Entry Approved | A form entry is approved |

Both triggers produce a `FormRecordOutput` containing the form ID, form name, record ID, state, timestamps, IP, member key, culture, and field values as JSON. Triggers can be filtered to specific forms via the settings, or left blank to match all forms.

## Actions

Run Forms operations from an Automate flow.

| Action | What it does |
|---|---|
| Submit Form | Programmatically submits a form entry with field values |
| Export Form Entries | Exports form entries as a JSON array (with optional approved-only filter and pagination) |

## How It Works

Umbraco Forms publishes notifications directly through Umbraco CMS's standard notification pipeline, so triggers subscribe to Forms notifications natively — no bridge handlers are required.

## Development

```bash
dotnet restore
dotnet build
dotnet test
```

### Project layout

```
src/
  Umbraco.Forms.Automate/            # Package source (triggers and actions)
tests/
  Umbraco.Forms.Automate.Tests.Unit/
demo/
  Umbraco.Forms.Automate.DemoSite/   # Demo Umbraco site
```

### NuGet feeds

This project uses [central package management](https://learn.microsoft.com/en-us/nuget/consume-packages/Central-Package-Management). The `Umbraco.Automate.*` packages are sourced from the Umbraco Nightly feed via `nuget.config`.

## License

MIT — see [LICENSE](LICENSE) for details.
