using Json.Schema;
using Umbraco.Forms.Automate.Triggers;
using Umbraco.Forms.Core.Models;
using Umbraco.Forms.Core.Persistence.Dtos;
using Umbraco.Forms.Core.Services;
using Record = Umbraco.Forms.Core.Persistence.Dtos.Record;

namespace Umbraco.Forms.Automate.Tests.Unit.Triggers;

public class FormFieldResolverTests
{
    private static Record RecordWith(params RecordField[] fields)
    {
        var record = new Record();
        foreach (var field in fields)
        {
            record.RecordFields[field.Key] = field;
        }

        return record;
    }

    private static RecordField RecordFieldWith(string alias, params object[] values)
        => new(new Field { Id = Guid.NewGuid(), Alias = alias })
        {
            Values = values.ToList(),
        };

    private static Form FormWith(Guid id, params Field[] fields)
        => new()
        {
            Id = id,
            Pages = new List<Page>
            {
                new()
                {
                    FieldSets = new List<FieldSet>
                    {
                        new() { Containers = new List<FieldsetContainer> { new() { Fields = fields.ToList() } } },
                    },
                },
            },
        };

    private static Field FormField(string alias, string caption)
        => new() { Id = Guid.NewGuid(), Alias = alias, Caption = caption };

    private static FormFieldResolver ResolverWith(params Form[] forms)
    {
        var service = new Mock<IFormService>();
        service.Setup(x => x.Get(It.IsAny<Guid[]>()))
            .Returns((Guid[] ids) => forms.Where(f => ids.Contains(f.Id)).ToList());
        return new FormFieldResolver(service.Object);
    }

    private static IReadOnlyDictionary<string, JsonSchema>? FieldProperties(JsonSchema schema)
        => schema.GetProperties()!.TryGetValue("fields", out var fields) ? fields.GetProperties() : null;

    // --- ExtractFields ---

    [Fact]
    public void ExtractFields_KeysByAlias_WithScalarValue()
    {
        var result = FormFieldResolver.ExtractFields(RecordWith(RecordFieldWith("email", "a@b.com")));

        result["email"].ShouldBe("a@b.com");
    }

    [Fact]
    public void ExtractFields_CamelCasesTheAlias()
    {
        var result = FormFieldResolver.ExtractFields(RecordWith(RecordFieldWith("FullName", "Alice")));

        result.Keys.ShouldContain("fullName");
    }

    [Fact]
    public void ExtractFields_MultiValueField_YieldsArray()
    {
        var result = FormFieldResolver.ExtractFields(RecordWith(RecordFieldWith("colours", "red", "green")));

        result["colours"].ShouldBe(new object[] { "red", "green" });
    }

    [Fact]
    public void ExtractFields_EmptyRecord_YieldsEmpty()
        => FormFieldResolver.ExtractFields(RecordWith()).ShouldBeEmpty();

    // --- ParseFormIds ---

    [Fact]
    public void ParseFormIds_Null_IsEmpty()
        => FormFieldResolver.ParseFormIds(null).ShouldBeEmpty();

    [Fact]
    public void ParseFormIds_Blank_IsEmpty()
        => FormFieldResolver.ParseFormIds("   ").ShouldBeEmpty();

    [Fact]
    public void ParseFormIds_SingleGuid_IsParsed()
    {
        var id = Guid.NewGuid();

        FormFieldResolver.ParseFormIds(id.ToString()).ShouldBe(new[] { id });
    }

    [Fact]
    public void ParseFormIds_CommaSeparated_AreParsed()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();

        FormFieldResolver.ParseFormIds($"{a},{b}").ShouldBe(new[] { a, b });
    }

    [Fact]
    public void ParseFormIds_JsonArray_AreParsed()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();

        FormFieldResolver.ParseFormIds($"[\"{a}\",\"{b}\"]").ShouldBe(new[] { a, b });
    }

    [Fact]
    public void ParseFormIds_Garbage_IsEmpty()
        => FormFieldResolver.ParseFormIds("not-a-guid").ShouldBeEmpty();

    // --- BuildOutputSchema ---

    [Fact]
    public void BuildOutputSchema_AlwaysIncludesEnvelope()
    {
        var schema = ResolverWith().BuildOutputSchema(null);

        schema.GetProperties()!.Keys.ShouldContain("ip");
        schema.GetProperties()!.Keys.ShouldContain("formName");
    }

    [Fact]
    public void BuildOutputSchema_NoFormsPicked_ExposesNoDynamicFields()
    {
        var schema = ResolverWith().BuildOutputSchema(null);

        // Either no 'fields' node, or one without enumerated properties.
        FieldProperties(schema).ShouldBeNull();
    }

    [Fact]
    public void BuildOutputSchema_OneForm_ExposesFieldsByCamelCaseAliasWithCaption()
    {
        var form = FormWith(Guid.NewGuid(), FormField("EmailAddress", "Email address"));
        var schema = ResolverWith(form).BuildOutputSchema(form.Id.ToString());

        var fields = FieldProperties(schema);
        fields!.Keys.ShouldContain("emailAddress");
        fields["emailAddress"].GetTitle().ShouldBe("Email address");
    }

    [Fact]
    public void BuildOutputSchema_MultipleForms_UnionsFields()
    {
        var contact = FormWith(Guid.NewGuid(), FormField("name", "Name"), FormField("email", "Email"));
        var newsletter = FormWith(Guid.NewGuid(), FormField("name", "Name"), FormField("company", "Company"));
        var schema = ResolverWith(contact, newsletter)
            .BuildOutputSchema($"{contact.Id},{newsletter.Id}");

        var fields = FieldProperties(schema);
        fields!.Keys.ShouldBe(new[] { "name", "email", "company" }, ignoreOrder: true);
    }
}
