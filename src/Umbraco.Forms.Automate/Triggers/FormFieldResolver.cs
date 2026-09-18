using System.Text.Json;
using Json.Schema;
using Json.Schema.Generation;
using Umbraco.Forms.Core;
using Umbraco.Forms.Core.Models;
using Umbraco.Forms.Core.Persistence.Dtos;
using Umbraco.Forms.Core.Services;

namespace Umbraco.Forms.Automate.Triggers;

/// <summary>
/// Resolves a form's fields into (a) the dynamic output schema shown in the
/// "Insert Binding Expression" picker and (b) the runtime output payload.
/// </summary>
public sealed class FormFieldResolver
{
    private static readonly SchemaGeneratorConfiguration EnvelopeConfig = new()
    {
        PropertyNameResolver = PropertyNameResolvers.CamelCase,
    };

    private readonly IFormService _formService;

    /// <summary>
    /// Initializes a new instance of the <see cref="FormFieldResolver"/> class.
    /// </summary>
    public FormFieldResolver(IFormService formService) => _formService = formService;

    /// <summary>
    /// Extracts a submitted record's field values, keyed by camel-cased field alias.
    /// Single-valued fields yield a scalar; multi-valued fields yield an array.
    /// </summary>
    /// <remarks>
    /// This overload cannot tell which fields the form marks as sensitive, so it returns
    /// every value. Use <see cref="ExtractFields(Record, Form)"/> instead.
    /// </remarks>
    [Obsolete("Use the overload taking the form, which omits fields marked as containing sensitive data. This overload returns every value and will be removed in a future major.")]
    public static IReadOnlyDictionary<string, object?> ExtractFields(Record record)
        => ExtractFields(record, sensitiveFieldIds: null);

    /// <summary>
    /// Extracts a submitted record's field values, keyed by camel-cased field alias,
    /// omitting any field the form marks as containing sensitive data.
    /// Single-valued fields yield a scalar; multi-valued fields yield an array.
    /// </summary>
    /// <param name="record">The submitted record.</param>
    /// <param name="form">The form the record belongs to, used to identify sensitive fields.</param>
    public static IReadOnlyDictionary<string, object?> ExtractFields(Record record, Form form)
        => ExtractFields(record, GetSensitiveFieldIds(form));

    /// <summary>
    /// Serializes a record's field values as JSON keyed by field Id, omitting any field the
    /// form marks as containing sensitive data.
    /// </summary>
    /// <remarks>
    /// Mirrors <see cref="Record.GenerateRecordDataAsJson"/> rather than calling it, because that
    /// method serializes every field on the record and the record must not be mutated here — it is
    /// the live instance Forms is in the middle of saving.
    /// </remarks>
    /// <param name="record">The submitted record.</param>
    /// <param name="form">The form the record belongs to, used to identify sensitive fields.</param>
    public static string GenerateRecordFieldsJson(Record record, Form form)
    {
        var sensitiveFieldIds = GetSensitiveFieldIds(form);

        var values = record.RecordFields.Values
            .Where(recordField => !sensitiveFieldIds.Contains(recordField.FieldId))
            .ToDictionary(recordField => recordField.FieldId, recordField => recordField.ValuesAsString(false));

        return JsonSerializer.Serialize(values, FormsJsonSerializerOptions.Default);
    }

    private static IReadOnlyDictionary<string, object?> ExtractFields(Record record, HashSet<Guid>? sensitiveFieldIds)
    {
        var fields = new Dictionary<string, object?>();

        foreach (var recordField in record.RecordFields.Values)
        {
            if (string.IsNullOrWhiteSpace(recordField.Alias))
            {
                continue;
            }

            if (sensitiveFieldIds?.Contains(recordField.FieldId) == true)
            {
                continue;
            }

            var key = JsonNamingPolicy.CamelCase.ConvertName(recordField.Alias);
            var values = recordField.Values;

            fields[key] = values switch
            {
                null or { Count: 0 } => null,
                { Count: 1 } => values[0],
                _ => values.ToArray(),
            };
        }

        return fields;
    }

    private static HashSet<Guid> GetSensitiveFieldIds(Form form)
        => [.. form.AllFields.Where(field => field.ContainsSensitiveData).Select(field => field.Id)];

    /// <summary>
    /// Builds the output JSON Schema for the binding-expression picker: the static
    /// envelope, with a <c>fields</c> object listing the union of the picked forms'
    /// fields (keyed by camel-cased alias). When no forms are picked, only the
    /// envelope is returned.
    /// </summary>
    /// <remarks>
    /// Fields marked as containing sensitive data are left out, because the runtime payload
    /// omits them — advertising them would offer bindings that always resolve to nothing.
    /// </remarks>
    public JsonSchema BuildOutputSchema(IReadOnlyList<Guid>? formIds)
    {
        var envelope = new JsonSchemaBuilder().FromType<FormRecordOutput>(EnvelopeConfig).Build();

        if (formIds is not { Count: > 0 })
        {
            return envelope;
        }

        var fieldProperties = new Dictionary<string, JsonSchema>();
        foreach (var field in _formService.Get([.. formIds]).SelectMany(form => form.AllFields))
        {
            if (string.IsNullOrWhiteSpace(field.Alias) || field.ContainsSensitiveData)
            {
                continue;
            }

            var key = JsonNamingPolicy.CamelCase.ConvertName(field.Alias);
            fieldProperties[key] = new JsonSchemaBuilder()
                .Type(SchemaValueType.String)
                .Title(string.IsNullOrWhiteSpace(field.Caption) ? key : field.Caption)
                .Build();
        }

        var properties = new Dictionary<string, JsonSchema>(envelope.GetProperties() ?? new Dictionary<string, JsonSchema>())
        {
            ["fields"] = new JsonSchemaBuilder()
                .Type(SchemaValueType.Object)
                .Properties(fieldProperties)
                .Build(),
        };

        return new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(properties)
            .Build();
    }
}
