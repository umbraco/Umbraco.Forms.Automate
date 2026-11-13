using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Bindings;
using Umbraco.Automate.Testing;
using Umbraco.Forms.Automate.Actions;
using Umbraco.Forms.Core.Data.Storage;
using Umbraco.Forms.Core.Enums;
using Umbraco.Forms.Core.Models;
using Umbraco.Forms.Core.Security;
using Umbraco.Forms.Core.Services;
using Record = Umbraco.Forms.Core.Persistence.Dtos.Record;

namespace Umbraco.Forms.Automate.Tests.Unit.Actions;

public class DeleteEntryActionTests
{
    private readonly Guid _formId = Guid.NewGuid();
    private readonly Guid _recordUniqueId = Guid.NewGuid();
    private readonly Form _form;

    private readonly Mock<IFormService> _formService = new();
    private readonly Mock<IRecordStorage> _recordStorage = new();
    private readonly Mock<IRecordService> _recordService = new();
    private readonly Mock<IFormsSecurity> _formsSecurity = new();

    public DeleteEntryActionTests()
    {
        _form = new Form { Id = _formId };
        _formService.Setup(s => s.Get(_formId)).Returns(_form);
        _formsSecurity.Setup(s => s.CanCurrentUserDeleteEntries()).Returns(true);
        _formsSecurity.Setup(s => s.HasAccessToForm(_formId)).Returns(true);
        _recordService.Setup(s => s.DeleteAsync(It.IsAny<Record>(), _form))
            .Callback<Record, Form>((r, _) => r.State = FormState.Deleted)
            .Returns(Task.CompletedTask);
    }

    [Theory]
    [InlineData("not-a-guid", null)]
    [InlineData(null, "not-a-guid")]
    public async Task InvalidIds_ReturnValidationErrorWithoutCheckingPermissions(string? formId, string? recordUniqueId)
    {
        var result = await ExecuteAsync(formId ?? _formId.ToString(), recordUniqueId ?? _recordUniqueId.ToString());

        result.Status.ShouldBe(ActionResultStatus.Failed);
        result.ErrorCategory.ShouldBe(StepRunErrorCategory.Validation);
        _formsSecurity.VerifyNoOtherCalls();
        _recordService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task BlankIds_UseTheEntryThatStartedTheRun()
    {
        var record = new Record { UniqueId = _recordUniqueId, Form = _formId };
        _recordStorage.Setup(s => s.GetRecordByUniqueId(_recordUniqueId, _form)).Returns(record);

        var result = await ExecuteAsync(" ", "", TriggerBindingData(_formId, _recordUniqueId));

        result.Status.ShouldBe(ActionResultStatus.Success);
        result.OutputData.ShouldBeOfType<DeleteEntryOutput>().Deleted.ShouldBeTrue();
        _recordService.Verify(s => s.DeleteAsync(record, _form), Times.Once);
    }

    [Fact]
    public async Task BlankIds_WithoutAFormTrigger_ReturnValidationErrorWithoutCheckingPermissions()
    {
        var result = await ExecuteAsync("", "", new Dictionary<string, object?>());

        result.Status.ShouldBe(ActionResultStatus.Failed);
        result.ErrorCategory.ShouldBe(StepRunErrorCategory.Validation);
        _formsSecurity.VerifyNoOtherCalls();
        _recordService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task WithoutDeleteEntriesPermission_ReturnsAuthenticationErrorWithoutLookingUpTheForm()
    {
        _formsSecurity.Setup(s => s.CanCurrentUserDeleteEntries()).Returns(false);

        var result = await ExecuteAsync();

        result.Status.ShouldBe(ActionResultStatus.Failed);
        result.ErrorCategory.ShouldBe(StepRunErrorCategory.Authentication);
        _formService.Verify(s => s.Get(It.IsAny<Guid>()), Times.Never);
        _recordService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task WithoutAccessToForm_ReturnsAuthenticationErrorWithoutLookingUpTheForm()
    {
        _formsSecurity.Setup(s => s.HasAccessToForm(_formId)).Returns(false);

        var result = await ExecuteAsync();

        result.Status.ShouldBe(ActionResultStatus.Failed);
        result.ErrorCategory.ShouldBe(StepRunErrorCategory.Authentication);
        _formService.Verify(s => s.Get(It.IsAny<Guid>()), Times.Never);
        _recordService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task FormNotFound_ReturnsValidationError()
    {
        _formService.Setup(s => s.Get(_formId)).Returns((Form?)null);

        var result = await ExecuteAsync();

        result.Status.ShouldBe(ActionResultStatus.Failed);
        result.ErrorCategory.ShouldBe(StepRunErrorCategory.Validation);
        _recordService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task EntryNotFound_SucceedsWithDeletedFalse()
    {
        _recordStorage.Setup(s => s.GetRecordByUniqueId(_recordUniqueId, _form)).Returns((Record?)null);

        var result = await ExecuteAsync();

        result.Status.ShouldBe(ActionResultStatus.Success);
        var output = result.OutputData.ShouldBeOfType<DeleteEntryOutput>();
        output.Deleted.ShouldBeFalse();
        output.FormId.ShouldBe(_formId);
        output.RecordUniqueId.ShouldBe(_recordUniqueId);
        _recordService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task EntryFound_DeletesThroughRecordServiceAndSucceeds()
    {
        var record = new Record { UniqueId = _recordUniqueId, Form = _formId };
        _recordStorage.Setup(s => s.GetRecordByUniqueId(_recordUniqueId, _form)).Returns(record);

        var result = await ExecuteAsync();

        result.Status.ShouldBe(ActionResultStatus.Success);
        result.OutputData.ShouldBeOfType<DeleteEntryOutput>().Deleted.ShouldBeTrue();
        _recordService.Verify(s => s.DeleteAsync(record, _form), Times.Once);
    }

    [Fact]
    public async Task DeleteCancelledByWorkflow_SucceedsWithDeletedFalse()
    {
        var record = new Record { UniqueId = _recordUniqueId, Form = _formId };
        _recordStorage.Setup(s => s.GetRecordByUniqueId(_recordUniqueId, _form)).Returns(record);
        _recordService.Setup(s => s.DeleteAsync(record, _form))
            .Callback<Record, Form>((r, _) => r.State = FormState.Approved)
            .Returns(Task.CompletedTask);

        var result = await ExecuteAsync();

        result.Status.ShouldBe(ActionResultStatus.Success);
        result.OutputData.ShouldBeOfType<DeleteEntryOutput>().Deleted.ShouldBeFalse();
    }

    private static Dictionary<string, object?> TriggerBindingData(Guid formId, Guid recordUniqueId) => new()
    {
        ["trigger"] = new Dictionary<string, object?>
        {
            ["formId"] = formId.ToString(),
            ["recordUniqueId"] = recordUniqueId.ToString(),
        },
    };

    private Task<ActionResult> ExecuteAsync(
        string? formId = null,
        string? recordUniqueId = null,
        IReadOnlyDictionary<string, object?>? bindingData = null)
        => ActionTestHarness.For<DeleteEntryAction>()
            .WithService(_formService.Object)
            .WithService(_recordStorage.Object)
            .WithService(_recordService.Object)
            .WithService(_formsSecurity.Object)
            .WithService(new BindingEvaluator(new BindingFilterCollection(() => [])))
            .WithBindingData(bindingData ?? new Dictionary<string, object?>())
            .WithSettings(new DeleteEntrySettings
            {
                FormId = formId ?? _formId.ToString(),
                RecordUniqueId = recordUniqueId ?? _recordUniqueId.ToString(),
            })
            .ExecuteAsync();
}
