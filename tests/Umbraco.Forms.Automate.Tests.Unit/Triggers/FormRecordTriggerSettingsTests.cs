using System.Text.Json;
using Umbraco.Forms.Automate.Triggers;

namespace Umbraco.Forms.Automate.Tests.Unit.Triggers;

/// <summary>
/// Covers how the stored form-picker value is read back into <see cref="FormRecordTriggerSettings"/>.
/// Mirrors the options Automate resolves settings with (camelCase, case-insensitive).
/// </summary>
public class FormRecordTriggerSettingsTests
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private static IReadOnlyList<Guid>? Deserialize(string formIdsJson)
        => JsonSerializer
            .Deserialize<FormRecordTriggerSettings>($"{{\"formIds\":{formIdsJson}}}", Options)!
            .FormIds;

    [Fact]
    public void MultipleFormsPicked_AreAllRead()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();

        Deserialize($"[\"{a}\",\"{b}\"]").ShouldBe(new[] { a, b });
    }

    [Fact]
    public void SingleFormPicked_IsRead()
    {
        var id = Guid.NewGuid();

        Deserialize($"[\"{id}\"]").ShouldBe(new[] { id });
    }

    [Fact]
    public void EmptyArray_IsEmpty()
        => Deserialize("[]").ShouldBeEmpty();

    [Fact]
    public void Null_IsNull()
        => Deserialize("null").ShouldBeNull();

    [Fact]
    public void MissingProperty_IsNull()
        => JsonSerializer.Deserialize<FormRecordTriggerSettings>("{}", Options)!.FormIds.ShouldBeNull();

    [Fact]
    public void LegacyScalarValue_IsRead()
    {
        var id = Guid.NewGuid();

        Deserialize($"\"{id}\"").ShouldBe(new[] { id });
    }

    [Fact]
    public void LegacyCommaSeparatedValue_IsRead()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();

        Deserialize($"\"{a},{b}\"").ShouldBe(new[] { a, b });
    }

    [Fact]
    public void BlankValue_IsEmpty()
        => Deserialize("\"   \"").ShouldBeEmpty();

    [Fact]
    public void UnparseableValue_IsEmptyRatherThanThrowing()
        => Deserialize("[\"not-a-guid\", 42, {}]").ShouldBeEmpty();

    [Fact]
    public void DuplicateIds_AreCollapsed()
    {
        var id = Guid.NewGuid();

        Deserialize($"[\"{id}\",\"{id}\"]").ShouldBe(new[] { id });
    }

    [Fact]
    public void RoundTrips_AsAnArray()
    {
        var id = Guid.NewGuid();
        var settings = new FormRecordTriggerSettings { FormIds = [id] };

        var json = JsonSerializer.Serialize(settings, Options);

        json.ShouldBe($"{{\"formIds\":[\"{id}\"]}}");
        JsonSerializer.Deserialize<FormRecordTriggerSettings>(json, Options)!.FormIds.ShouldBe(new[] { id });
    }
}
