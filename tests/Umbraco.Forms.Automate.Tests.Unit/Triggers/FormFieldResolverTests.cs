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

    private static RecordField RecordFieldFor(Field field, params object[] values)
        => new(field) { Values = values.ToList() };

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

    private static Field FormField(string alias, string caption, bool containsSensitiveData = false)
        => new() { Id = Guid.NewGuid(), Alias = alias, Caption = caption, ContainsSensitiveData = containsSensitiveData };

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
        var email = FormField("email", "Email");
        var result = FormFieldResolver.ExtractFields(
            RecordWith(RecordFieldFor(email, "a@b.com")),
            FormWith(Guid.NewGuid(), email));

        result["email"].ShouldBe("a@b.com");
    }

    [Fact]
    public void ExtractFields_CamelCasesTheAlias()
    {
        var fullName = FormField("FullName", "Full name");
        var result = FormFieldResolver.ExtractFields(
            RecordWith(RecordFieldFor(fullName, "Alice")),
            FormWith(Guid.NewGuid(), fullName));

        result.Keys.ShouldContain("fullName");
    }

    [Fact]
    public void ExtractFields_MultiValueField_YieldsArray()
    {
        var colours = FormField("colours", "Colours");
        var result = FormFieldResolver.ExtractFields(
            RecordWith(RecordFieldFor(colours, "red", "green")),
            FormWith(Guid.NewGuid(), colours));

        result["colours"].ShouldBe(new object[] { "red", "green" });
    }

    [Fact]
    public void ExtractFields_EmptyRecord_YieldsEmpty()
        => FormFieldResolver.ExtractFields(RecordWith(), FormWith(Guid.NewGuid())).ShouldBeEmpty();

    [Fact]
    public void ExtractFields_OmitsFieldsMarkedAsSensitive()
    {
        var email = FormField("email", "Email");
        var nationalId = FormField("nationalId", "National ID", containsSensitiveData: true);
        var record = RecordWith(RecordFieldFor(email, "a@b.com"), RecordFieldFor(nationalId, "123456"));

        var result = FormFieldResolver.ExtractFields(record, FormWith(Guid.NewGuid(), email, nationalId));

        result.Keys.ShouldBe(["email"]);
        result["email"].ShouldBe("a@b.com");
    }

    [Fact]
    public void ExtractFields_KeepsFieldsWhenNoneAreSensitive()
    {
        var email = FormField("email", "Email");
        var name = FormField("name", "Name");
        var record = RecordWith(RecordFieldFor(email, "a@b.com"), RecordFieldFor(name, "Alice"));

        var result = FormFieldResolver.ExtractFields(record, FormWith(Guid.NewGuid(), email, name));

        result.Keys.ShouldBe(["email", "name"], ignoreOrder: true);
    }

    // --- GenerateRecordFieldsJson ---

    [Fact]
    public void GenerateRecordFieldsJson_OmitsFieldsMarkedAsSensitive()
    {
        var email = FormField("email", "Email");
        var nationalId = FormField("nationalId", "National ID", containsSensitiveData: true);
        var record = RecordWith(RecordFieldFor(email, "a@b.com"), RecordFieldFor(nationalId, "123456"));

        var json = FormFieldResolver.GenerateRecordFieldsJson(record, FormWith(Guid.NewGuid(), email, nationalId));

        json.ShouldContain(email.Id.ToString());
        json.ShouldContain("a@b.com");
        json.ShouldNotContain(nationalId.Id.ToString());
        json.ShouldNotContain("123456");
    }

    [Fact]
    public void GenerateRecordFieldsJson_KeepsFieldsWhenNoneAreSensitive()
    {
        var email = FormField("email", "Email");
        var record = RecordWith(RecordFieldFor(email, "a@b.com"));

        var json = FormFieldResolver.GenerateRecordFieldsJson(record, FormWith(Guid.NewGuid(), email));

        json.ShouldContain(email.Id.ToString());
        json.ShouldContain("a@b.com");
    }

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
        var schema = ResolverWith(form).BuildOutputSchema([form.Id]);

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
            .BuildOutputSchema([contact.Id, newsletter.Id]);

        var fields = FieldProperties(schema);
        fields!.Keys.ShouldBe(new[] { "name", "email", "company" }, ignoreOrder: true);
    }

    [Fact]
    public void BuildOutputSchema_OmitsFieldsMarkedAsSensitive()
    {
        var form = FormWith(
            Guid.NewGuid(),
            FormField("email", "Email"),
            FormField("nationalId", "National ID", containsSensitiveData: true));

        var schema = ResolverWith(form).BuildOutputSchema([form.Id]);

        var fields = FieldProperties(schema);
        fields!.Keys.ShouldBe(["email"]);
    }
}
