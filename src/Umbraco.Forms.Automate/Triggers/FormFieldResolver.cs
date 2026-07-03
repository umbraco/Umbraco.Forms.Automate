using System.Text.Json;
using Json.Schema;
using Json.Schema.Generation;
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
    public static IReadOnlyDictionary<string, object?> ExtractFields(Record record)
    {
        var fields = new Dictionary<string, object?>();

        foreach (var recordField in record.RecordFields.Values)
        {
            if (string.IsNullOrWhiteSpace(recordField.Alias))
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

    /// <summary>
    /// Parses the trigger's stored form-picker value into distinct form ids.
    /// Tolerates comma-separated and JSON-array storage formats.
    /// </summary>
    public static IReadOnlyList<Guid> ParseFormIds(string? formIds)
    {
        if (string.IsNullOrWhiteSpace(formIds))
        {
            return [];
        }

        var ids = new List<Guid>();

        foreach (var token in formIds.Split([',', '[', ']', '"', ' '], StringSplitOptions.RemoveEmptyEntries))
        {
            if (Guid.TryParse(token, out var id) && !ids.Contains(id))
            {
                ids.Add(id);
            }
        }

        return ids;
    }

    /// <summary>
    /// Builds the output JSON Schema for the binding-expression picker: the static
    /// envelope, with a <c>fields</c> object listing the union of the picked forms'
    /// fields (keyed by camel-cased alias). When no forms are picked, only the
    /// envelope is returned.
    /// </summary>
    public JsonSchema BuildOutputSchema(string? formIds)
    {
        var envelope = new JsonSchemaBuilder().FromType<FormRecordOutput>(EnvelopeConfig).Build();

        var ids = ParseFormIds(formIds);
        if (ids.Count == 0)
        {
            return envelope;
        }

        var fieldProperties = new Dictionary<string, JsonSchema>();
        foreach (var field in _formService.Get([.. ids]).SelectMany(form => form.AllFields))
        {
            if (string.IsNullOrWhiteSpace(field.Alias))
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
