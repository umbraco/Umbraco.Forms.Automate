# Umbraco.Forms.Automate

Umbraco Forms triggers and actions for [Umbraco Automate](https://umbraco.com/products/umbraco-automate/), enabling workflow automation based on form submissions and record management.

## Triggers

| Alias | Name | Description |
|---|---|---|
| `umbracoForms.formSubmitted` | Form Submitted | Fires when a form entry is submitted |
| `umbracoForms.formEntryApproved` | Form Entry Approved | Fires when a form entry is approved |

Both triggers produce a `FormRecordOutput` containing the form ID, form name, record ID, state, timestamps, IP, member key, culture, and field values as JSON.

Triggers can be filtered to specific forms via the settings, or left blank to match all forms.

## Actions

| Alias | Name | Description |
|---|---|---|
| `umbracoForms.submitForm` | Submit Form | Programmatically submits a form entry with field values |
| `umbracoForms.exportEntries` | Export Form Entries | Exports form entries as a JSON array (with optional approved-only filter and pagination) |

## Prerequisites

- .NET 10+
- Umbraco CMS with Umbraco Forms 17.3+
- Umbraco Automate

## Getting started

Install the NuGet package into your Umbraco project:

```
dotnet add package Umbraco.Forms.Automate
```

The triggers and actions are automatically discovered by Umbraco Automate.

## Solution structure

```
src/
  Umbraco.Forms.Automate/          # Core library (triggers and actions)
tests/
  Umbraco.Forms.Automate.Tests.Unit/ # Unit tests (xUnit, Shouldly, Moq)
demo/
  Umbraco.Forms.Automate.DemoSite/  # Demo Umbraco site
```

## Development

### Build

```
dotnet build
```

### Test

```
dotnet test
```

### NuGet feeds

This project uses [central package management](https://learn.microsoft.com/en-us/nuget/consume-packages/Central-Package-Management). The `Umbraco.Automate.*` packages are sourced from the [Umbraco Nightly feed](https://www.myget.org/F/umbraconightly/api/v3/index.json) via `nuget.config`.

## Contributing

This project is managed by Umbraco HQ. For details on contributing, please refer to the [Umbraco contribution guidelines](https://github.com/umbraco/.github/blob/main/.github/CONTRIBUTING.md).

## License

See [LICENSE.md](LICENSE.md) for details.
