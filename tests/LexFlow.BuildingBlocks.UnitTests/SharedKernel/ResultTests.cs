using LexFlow.SharedKernel.Results;

namespace LexFlow.BuildingBlocks.UnitTests.SharedKernel;

public sealed class ResultTests
{
    private static readonly Error NotFound = Error.NotFound("case.not_found", "Processo não encontrado.");

    [Fact]
    public void Success_has_no_error()
    {
        Result.Success.IsSuccess.ShouldBeTrue();
        Result.Success.Error.ShouldBeNull();
    }

    [Fact]
    public void Implicit_value_conversion_creates_success()
    {
        Result<int> result = 42;

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(42);
    }

    [Fact]
    public void Implicit_error_conversion_creates_failure()
    {
        Result<int> result = NotFound;

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(NotFound);
    }

    [Fact]
    public void Reading_value_of_failure_throws()
    {
        Result<int> result = NotFound;

        Should.Throw<InvalidOperationException>(() => result.Value).Message.ShouldContain("case.not_found");
    }

    [Fact]
    public void Map_transforms_success_and_propagates_failure()
    {
        Result<int> ok = 2;
        Result<int> failed = NotFound;

        ok.Map(v => v * 10).Value.ShouldBe(20);
        failed.Map(v => v * 10).Error.ShouldBe(NotFound);
    }

    [Theory]
    [InlineData(ErrorType.Validation)]
    [InlineData(ErrorType.NotFound)]
    [InlineData(ErrorType.Conflict)]
    [InlineData(ErrorType.Forbidden)]
    [InlineData(ErrorType.BusinessRule)]
    public void Error_factories_set_type(ErrorType type)
    {
        var error = type switch
        {
            ErrorType.Validation => Error.Validation("x.y", "m"),
            ErrorType.NotFound => Error.NotFound("x.y", "m"),
            ErrorType.Conflict => Error.Conflict("x.y", "m"),
            ErrorType.Forbidden => Error.Forbidden("x.y", "m"),
            _ => Error.BusinessRule("x.y", "m"),
        };

        error.Type.ShouldBe(type);
    }

    [Fact]
    public void Validation_error_carries_field_errors()
    {
        var error = new ValidationError([new FieldError("title", "case.title_required", "Informe o título.")]);

        error.Code.ShouldBe(ValidationError.DefaultCode);
        error.Type.ShouldBe(ErrorType.Validation);
        error.Errors.ShouldHaveSingleItem().Field.ShouldBe("title");
    }
}
