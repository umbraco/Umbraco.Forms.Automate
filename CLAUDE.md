# Umbraco.Forms.Automate

Satellite add-on package for [Umbraco.Automate](https://umbraco.com/products/umbraco-automate/) (Umbraco's
core automation/workflow product). This repo contributes **Umbraco Forms**-specific triggers and actions —
it does not run standalone; it is a thin extension loaded into a host site that already has both
Umbraco CMS + Umbraco Automate + Umbraco Forms installed.

Repo: `github.com/umbraco/Umbraco.Forms.Automate`. Azure DevOps project/pipeline: "Umbraco Forms" / 717.

## Architecture

Single small class library, `src/Umbraco.Forms.Automate/Umbraco.Forms.Automate.csproj` (targets `net10.0`,
set via the shared `Directory.Build.props`). Two package dependencies define the whole surface area:
`Umbraco.Automate.Core` (the trigger/action/settings SDK) and `Umbraco.Forms.Core` (Forms' domain model
and services). There is no web project, no controllers, no persistence layer in this repo — everything is
DI-registered classes discovered by the Automate host at startup.

```
src/Umbraco.Forms.Automate/
  Constants.cs                          Sections.Forms = "forms" (RequiredSections gate)
  FormsAutomateComposer.cs              IComposer — DI registration entry point
  Triggers/
    DynamicFormRecordTriggerBase.cs     shared generic base for both triggers
    FormSubmittedTrigger.cs             umbracoForms.formSubmitted
    FormEntryApprovedTrigger.cs         umbracoForms.formEntryApproved
    FormFieldResolver.cs                dynamic schema + runtime field extraction (used by both triggers)
    FormRecordOutput.cs                 shared trigger output DTO
    FormRecordTriggerSettings.cs        shared trigger settings DTO (form-id filter)
  Actions/
    SubmitFormAction.cs + Settings/Output    umbracoForms.submitForm
    ExportEntriesAction.cs + Settings/Output umbracoForms.exportEntries
```

### Composition root

`FormsAutomateComposer` (`src/Umbraco.Forms.Automate/FormsAutomateComposer.cs:14-16`) is the *only* DI
wiring in the package — it registers `FormFieldResolver` as transient. It does **not** register the
triggers or actions themselves; those are picked up by Umbraco Automate's own reflection-based discovery
(driven by the `[Trigger(...)]` / `[Action(...)]` attributes — see README "Zero configuration"). If you add
a new trigger/action class with the right attribute and base type, it is automatically live — no composer
edit needed. Only add a composer registration when a class needs a DI-injected collaborator that isn't
itself trigger/action infrastructure (as `FormFieldResolver` is, since it's a plain shared service, not a
`StepType`).

### Triggers: dynamic output schema

Both triggers inherit `DynamicFormRecordTriggerBase<TNotification>`
(`src/Umbraco.Forms.Automate/Triggers/DynamicFormRecordTriggerBase.cs:13-33`), which itself extends
Automate Core's `DynamicOutputNotificationTriggerBase<FormRecordTriggerSettings, TNotification>`. This is
the mechanism behind the "Insert Binding Expression" picker showing `fields.<alias>` entries per form:

- `FormFieldResolver.BuildOutputSchema(formIds)` (`Triggers/FormFieldResolver.cs:63-100`) builds a JSON
  Schema: the static `FormRecordOutput` envelope, plus — only when one or more forms are picked in the
  trigger's settings — a `fields` object unioning the picked forms' field aliases (camelCased, titled by
  the field's caption). This schema is what the flow-editor UI reads at *design time*.
- `FormFieldResolver.ExtractFields(record)` (`Triggers/FormFieldResolver.cs:32-55`) extracts the actual
  submitted values at *runtime*, keyed the same way. Single-valued fields yield a scalar; multi-valued
  fields (checkboxes etc.) yield an array.
- These two methods must stay in lock-step: the schema advertises keys, `ExtractFields` must populate the
  same keys the same way (both camelCase the alias via `JsonNamingPolicy.CamelCase` /
  `PropertyNameResolvers.CamelCase` — do not switch one without the other).
- Leaving the form filter blank matches all forms at runtime, but the *design-time* schema then only shows
  the static envelope — there's no way to enumerate "fields of all forms" in the picker. This is
  intentional (README "How It Works" / "Triggers" section), not a bug to fix.

Both triggers subscribe to Umbraco Forms' own CMS notifications
(`RecordSubmittedNotification`, `RecordApprovedNotification` from
`Umbraco.Forms.Core.Services.Notifications`) directly — there is no bridge/adapter layer. `MapEvent`
(e.g. `Triggers/FormSubmittedTrigger.cs:27-51`) is a 1:1 notification-to-`TriggerEvent` mapper.

### Actions

- `SubmitFormAction` (`Actions/SubmitFormAction.cs`) — validates the form GUID and `FieldValuesJson`
  (a JSON object of **field-GUID → string value**, not alias → value — this differs from the trigger
  output shape, which is alias-keyed), builds a `Record`/`RecordField` graph, and calls
  `IRecordService.SubmitAsync`. All failure paths return `ActionResult.Failed(..., StepRunErrorCategory.Validation)` — there is no "system/unexpected error" category used here; malformed input,
  missing form, and bad JSON are all treated as user/config errors, not runtime failures.
- `ExportEntriesAction` (`Actions/ExportEntriesAction.cs`) — reads via `IRecordReaderService`
  (`GetRecordsFromForm` or `GetApprovedRecordsFromForm` depending on the `ApprovedOnly` toggle), clamps
  `PageSize` to `[1, 500]` (`Actions/ExportEntriesAction.cs:44`), and hand-builds a `JsonArray` by parsing
  each record's own `GenerateRecordDataAsJson()` output rather than deserializing-then-reserializing —
  intentional to avoid an unnecessary round-trip (see the comment at `Actions/ExportEntriesAction.cs:50-51`).

### Constants and section gating

`Constants.Sections.Forms = "forms"` (`src/Umbraco.Forms.Automate/Constants.cs:18`) is applied via
`RequiredSections = [Constants.Sections.Forms]` on every `[Trigger]`/`[Action]` attribute. This hides the
step type from the Automate flow editor for users without access to the Forms backoffice section — since
these steps handle PII (IP address, member key, field values), don't remove this gate when adding new
step types in this package.

## Commands

All commands run from the repo root unless noted. `global.json` pins the .NET SDK to `10.0.100`
(`rollForward: latestFeature`).

```bash
dotnet restore Umbraco.Forms.Automate.slnx
dotnet build Umbraco.Forms.Automate.slnx --configuration Release
dotnet test Umbraco.Forms.Automate.slnx --configuration Release
```

Run a single test class/method:

```bash
dotnet test tests/Umbraco.Forms.Automate.Tests.Unit/Umbraco.Forms.Automate.Tests.Unit.csproj --filter "FullyQualifiedName~FormFieldResolverTests"
```

Pack (mirrors CI's `dotnet pack`, `ContinuousIntegrationBuild`/version handled by Nerdbank.GitVersioning
via `nbgv` in CI — a plain local pack still works but won't get a clean NBGV-derived version unless you've
run `nbgv` yourself):

```bash
dotnet pack Umbraco.Forms.Automate.slnx --configuration Release --no-build --output ./artifacts
```

Run the demo site (Umbraco backoffice + a working Forms + Automate install for manual verification):

```bash
cd demo/Umbraco.Forms.Automate.DemoSite
dotnet run
```

The demo site's Kestrel listener is customized by
`demo/Umbraco.Forms.Automate.DemoSite/Composers/NamedPipeListenerComposer.cs` — in `Development` only, it
adds a named pipe (Windows) / Unix socket listener whose name is derived from the current git branch or
worktree folder name (`GetUniqueIdentifier()`, `Composers/NamedPipeListenerComposer.cs:100-145`), plus a
`GET /site-address` endpoint that returns the resolved HTTPS URL as plain text. This exists so tooling can
find/attach to a *specific* running worktree's demo instance without port-scanning — relevant because this
repo keeps persistent worktrees under `.claude/worktrees/` (`support-17.x`, and ad-hoc feature worktrees).
This is dev-only infrastructure, not part of the shipped package.

## Style Guide

Nothing unusual beyond standard .NET/Umbraco conventions already enforced by the codebase:
`Nullable` + `ImplicitUsings` enabled repo-wide (`Directory.Build.props:9-11`), `GenerateDocumentationFile`
is on for the main project (off for the test project — `tests/.../*.csproj:5`), and every public member in
`src/` has an XML doc comment. Match that when adding new triggers/actions/settings/outputs — public
surface without a `<summary>` will stand out against the rest of the package.

One naming subtlety worth preserving: output/DTO properties that hold values from all-caps domain
concepts are named as normal PascalCase words, not acronyms — e.g. `FormRecordOutput.Ip` (not `IP`),
`RecordUniqueId` (not `RecordUId`/`RecordGUID`). See **Edge Cases** below for why this matters beyond
style.

## Test Bench

`tests/Umbraco.Forms.Automate.Tests.Unit/` — xUnit + Shouldly + Moq, mirroring `src/`'s
`Actions/`/`Triggers/` split. Global usings for `Xunit`, `Shouldly`, `Moq` are set in the test csproj
(`tests/.../Umbraco.Forms.Automate.Tests.Unit.csproj:28-32`), so test files don't need those `using`
statements.

Two different test styles are used depending on what's under test — don't mix them up:

- **Actions** are tested through `Umbraco.Automate.Testing`'s `ActionTestHarness.For<TAction>()` fluent
  builder (`.WithService(mock.Object)`, `.WithSettings(settings)`, `.ExecuteAsync()`) — see
  `tests/.../Actions/SubmitFormActionTests.cs`. This harness resolves the action's constructor
  dependencies from the registered mocks, so you don't hand-construct the action.
- **Triggers** are constructed directly in the test (`new FormSubmittedTrigger(new
  TriggerInfrastructure(Mock.Of<IEditableModelResolver>()), new FormFieldResolver(mock.Object))`) — there
  is no trigger equivalent of `ActionTestHarness` in this codebase currently. If one gets added upstream in
  `Umbraco.Automate.Testing`, prefer it for consistency, but don't invent a local abstraction for it.

`FormFieldResolverTests` builds `Form`/`Field`/`Record` object graphs by hand (see the `FormWith`/`RecordWith`/`RecordFieldWith` helpers at the top of
`tests/.../Triggers/FormFieldResolverTests.cs:12-46`) — Umbraco Forms' domain model has no public test
builders, so these ad-hoc helpers are the closest thing to a fixture. Reuse/extend them rather than
constructing `Form`/`Record` graphs inline in new tests.

CI runs the full test project three times — Windows, Linux, macOS — via a matrix strategy
(`.devops/test.yml:8-15`), against the artifact built once in the `Build` stage (not rebuilt per-OS). Keep
tests OS-agnostic (no hardcoded path separators, no OS-specific line endings assumptions).

## Error Handling

Actions follow one convention: on any input/config problem, return
`ActionResult.Failed(exception, StepRunErrorCategory.Validation)` rather than throwing (see
`Actions/SubmitFormAction.cs:42-53,65-68` and `Actions/ExportEntriesAction.cs:39-42`). There is currently no
example in this repo of `StepRunErrorCategory` values other than `Validation` — if you add an action that
can fail for infrastructure reasons (e.g. Forms service unavailable) distinct from bad user input, check
`Umbraco.Automate.Core`'s `StepRunErrorCategory` enum for the right category rather than defaulting
everything to `Validation`.

Triggers have no error-handling story of their own in this package — `MapEvent` is a synchronous, pure
mapping from a CMS notification to a `TriggerEvent`; if the notification's data is malformed that's a
Forms-side bug, not something this package defends against.

## Clean Code

- `FormSubmittedTrigger.MapEvent` and `FormEntryApprovedTrigger.MapEvent` are near-identical (both build a
  `FormRecordOutput` the same way) — this duplication is deliberate, not an oversight: the notification
  types differ (`RecordSubmittedNotification` vs `RecordApprovedNotification`) and `DynamicFormRecordTriggerBase<TNotification>` already factors out everything that *can* be shared (the settings type,
  the dynamic schema resolution). If you add a third trigger with the same output shape, resist adding a
  shared `MapEvent` helper unless a third near-identical copy actually appears — two is not yet a pattern
  here given the base class already absorbs the schema-building complexity.
- `FormFieldResolver` is intentionally the single place that understands "how a Forms field alias becomes
  an Automate binding key." Don't reimplement alias→camelCase conversion elsewhere (e.g. inline in a new
  action) — call into `FormFieldResolver` (making methods `static`/injectable as needed) so the mapping
  stays in one place if the convention ever changes.

## Security

- `RequiredSections = [Constants.Sections.Forms]` on every trigger/action (see **Architecture** above) is
  the access-control boundary for this package — it gates visibility in the flow editor for users without
  Forms section access, because trigger/action output surfaces PII (submitter IP, member key, raw field
  values). Any new step type touching form records must carry this attribute.
- `SubmitFormAction.FieldValuesJson` and `ExportEntriesAction` output both move raw form data (potentially
  PII) through Automate flow bindings/logs. This package doesn't redact or filter — that's a deliberate
  scope boundary (Forms already governs field-level data classification), but be aware if you're asked to
  add e.g. an audit trail or redaction feature that it doesn't exist today.
- Field-value JSON in `SubmitFormAction` is deserialized with `System.Text.Json` and guarded by a
  `try/catch (JsonException)` (`Actions/SubmitFormAction.cs:56-68`) before ever touching Forms services —
  don't remove that guard when refactoring; unguarded `Deserialize` on user/binding-supplied JSON is the
  one place in this package that takes untrusted string input directly.

## Teamwork / Workflow

**Branch model** (shared across all `Umbraco.*.Automate` satellites):
- `main` = current CMS major line (v18).
- `support/17.x` = previous CMS major line (v17), checked out as a persistent git worktree at
  `.claude/worktrees/support-17.x`.
- Releases are cut as `release/YYYY.MM.N` branches, where `N` is a single counter shared across **both**
  lines for the month (e.g. `release/2026.07.1` for v18 and `release/2026.07.2` for v17 in the same
  month — not independent per-line counters).

**PRs**: `azure-pipelines.yml:12-16` triggers PR validation only against `main` and `dev` (there's no `dev`
branch currently checked out locally, but the trigger config includes it — likely vestigial from a shared
template; don't be surprised if `dev` doesn't exist). Recent history shows small, single-purpose PRs
merged via GitHub PR (`Merge pull request #9 from umbraco/feature/dynamic-form-fields`, etc.), with fix
branches following `fix/<short-description>` and backport branches following
`backport/<short-description>-<major>` (e.g. `backport/ip-field-alias-17`) — see **Edge Cases** for the
concrete example this pattern came from.

**CI** (`azure-pipelines.yml` → `.devops/build-and-pack.yml` + `.devops/test.yml`): triggers on push to
`main`, `dev`, `release/*`, `hotfix/*`, `feature/*`. Two stages only — `Build` (restore, `nbgv cloud` to
stamp the version, build, pack, publish the `.nupkg` + full source tree as pipeline artifacts, optional
CycloneDX SBOM generation/upload to Dependency-Track) and `Test` (downloads the `build_output` artifact,
runs `dotnet test` on a Windows/Linux/macOS matrix). **There is no Publish stage** — pushing the built
package to the MyGet feed is a manual step a human does after CI goes green. Nothing in this repo's
automation pushes packages anywhere.

**Release process** — two Claude Code skills automate the mechanics precisely; read them rather than this
summary for exact commands:
- `.claude/skills/release-management/SKILL.md` — cuts a `release/YYYY.MM.N` branch from `main` or
  `support/17.x`, bumps `Directory.Packages.props`'s `Umbraco.Automate*` ranges and `version.json` to the
  target major's stable floor (`[X.0.0, X.999.999)`).
- `.claude/skills/post-release-cleanup/SKILL.md` — run only after CI is green **and** a human has manually
  confirmed the MyGet push; merges `--no-ff` back into the target branch, tags `release-<version>`, creates
  a GitHub Release via `gh release create <tag> --target <branch> --generate-notes`, patch-bumps
  `version.json` on the target branch for next-cycle nightlies, and deletes the release branch
  (local + remote).

`nuget.config` sources nuget.org + the Umbraco Nightly and Umbraco Prereleases MyGet feeds, with package
source mapping restricting `Umbraco*` packages to the Umbraco feeds specifically
(`nuget.config:9-23`) — this is how `main` normally resolves `Umbraco.Automate.Core`'s floating
`18.0.0-*`/nightly version outside of a release branch.

## Edge Cases

- **The `ip` casing bug is the canonical gotcha in this codebase.** Commit `917bdc4` ("fix: Use lowercase
  'ip' alias for form record IP output field") had to fix a real production issue: `System.Text.Json`'s
  `JsonNamingPolicy.CamelCase` (used both by `FormFieldResolver.BuildOutputSchema`'s envelope generation
  and by the runtime output) camelCases `"IP"` to `"iP"`, not `"ip"` — because the naming policy only
  lowercases the *leading run* of a PascalCase word, and a two-letter all-caps acronym has no lowercase
  tail to anchor on. `FormRecordOutput.Ip` is named `Ip` (title case, not `IP`) specifically so it
  camelCases cleanly to `ip`. `FormSubmittedTriggerTests.OutputSchema_ExposesIpFieldWithLowercaseAlias`
  (`tests/.../Triggers/FormSubmittedTriggerTests.cs:44-55`) is a regression test for exactly this. **Any
  new output property whose logical name is an acronym must be named as a normal word (`Ip`, not `IP`;
  `Url`, not `URL`) or it will silently break the binding-expression picker's advertised key vs. the
  runtime payload's actual key.**
- This fix had to be **backported** to v17 as `backport/ip-field-alias-17` (still visible as a remote
  branch) — a reminder that CMS-version-specific behavior/bug fixes discovered on `main` (v18) generally
  need a matching fix cherry-picked into `support/17.x`, even though the two lines otherwise diverge only
  in dependency version ranges.
- **`FormRecordTriggerSettings.FormIds` must stay a collection.** It was originally a `string`, which made
  Automate Core's `SingleValueArrayConverterFactory` throw on any selection of two or more forms — and
  because trigger settings are resolved on the dispatch path, that one automation's exception stopped
  *every* automation using a form trigger (umbraco/Umbraco.Automate#219). `FormIdsJsonConverter`
  (`Triggers/FormIdsJsonConverter.cs`) reads the property, tolerating the JSON-array shape the picker
  (`Forms.PropertyEditorUi.FormPicker.Multiple`) persists plus the single-value and comma-separated shapes
  older automations may hold, and dropping unparseable tokens instead of throwing. Don't "simplify" it to
  one shape, and don't make the property non-nullable — a non-nullable reference type gets an implicit
  `[Required]` from `EditableModelSchemaBuilder`, which would mark an optional filter as required in the UI.
- The demo site (`demo/`) deliberately opts *out* of central package management
  (`demo/Directory.Packages.props: ManagePackageVersionsCentrally=false`) and disables package validation
  (`demo/Directory.Build.props: EnablePackageValidation=false`) — its own `.csproj` pins concrete versions
  directly, including a **floating prerelease** `Umbraco.Automate` (`18.0.0-*`), unlike the root
  `Directory.Packages.props`'s stable-range approach used for the shipped package. This is intentional: the
  demo needs to run against whatever the latest Automate nightly is, not a pinned release range. Building
  the demo site can regenerate an untracked `appsettings-schema*.json` file
  (`demo/Umbraco.Forms.Automate.DemoSite/appsettings-schema*.json`) — leave it uncommitted if you notice it
  dirty in `git status`.

## Agentic Workflow

- This is a small, single-product repo — most tasks touch exactly one trigger or action file plus its
  paired settings/output DTO and its test file. Read the sibling trigger/action first (there are only two
  of each) before writing a new one; the pattern is consistent enough that deviating without a reason will
  look wrong in review.
- Before changing anything under `Triggers/`, re-read `FormFieldResolver.cs` in full — it's the one file
  where design-time schema and runtime payload must be kept in sync by hand (see **Architecture**).
- Don't add a composer registration for a new trigger/action class itself — only for genuinely new shared
  services those classes depend on (see **Architecture** → Composition root).
- When running tests or the demo site locally, be aware of the persistent worktrees at
  `.claude/worktrees/support-17.x` (and possibly other feature worktrees) — confirm which checkout you're
  actually in before assuming `main`-line dependency versions apply.
- Do not touch `Directory.Packages.props`'s `Umbraco.Automate*`/`Umbraco.Forms.Core` version ranges or
  `version.json` outside of the release-management skill's workflow — those are release-process state, not
  everyday dependency bumps.

## Project-Specific Notes

- **This package has no runtime footprint of its own beyond DI registration.** There's no controller, no
  background service, no scheduled job in `src/`. Everything it does happens either (a) synchronously
  inside a CMS notification handler (`MapEvent`) when Forms raises `RecordSubmittedNotification`/
  `RecordApprovedNotification`, or (b) synchronously inside an `ActionBase.ExecuteAsync` call made by
  Automate's own flow-run engine. There is nothing in this repo that polls, schedules, or runs
  independently — all execution is driven by the host (Forms notifications or Automate flow runs).
- **Integration surface with Umbraco Forms** is exactly three services: `IFormService` (form definitions —
  used by both `FormFieldResolver` and `SubmitFormAction`), `IRecordService` (write path —
  `SubmitFormAction`), and `IRecordReaderService` (read path — `ExportEntriesAction`). If Forms' SDK adds a
  new capability you need, check whether it belongs on one of these existing services before reaching for a
  new one — the package currently keeps its Forms dependency surface intentionally narrow.
- **Known limitation, not a bug**: the binding-expression picker cannot show field suggestions for "all
  forms" — leaving a trigger's form filter blank matches every form at runtime but only exposes the static
  envelope at design time (see **Architecture** → Triggers). This is called out explicitly in the README
  ("Triggers" section) as expected behavior, not something to silently "fix" by e.g. unioning every form's
  fields — that would likely make the schema huge and mostly irrelevant per-flow.
- **`ExportEntriesAction` is not currently paginated across multiple calls** — it takes a single
  `PageSize` (clamped to 500) and returns page 1 only; there's no continuation-token/cursor output. If a
  future request asks for "export all entries," that's a real feature gap, not something already handled
  by a hidden loop.
- The package ships with a marketplace listing (`umbraco-marketplace.json`,
  `umbraco-marketplace-readme.md`) declaring it as `IsSubPackageOf: Umbraco.Forms` — keep those two files
  in sync with README changes if the feature set (trigger/action count, names) changes; they're
  marketplace-facing metadata, not code, but are easy to forget when the file list is skimmed.
