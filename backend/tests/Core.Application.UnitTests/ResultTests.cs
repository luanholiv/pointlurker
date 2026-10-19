using FluentAssertions;
using PointLurker.Core.Application;

namespace Core.Application.UnitTests;

public sealed class ResultTests
{
    private readonly record struct TestError(string Code, string Message);

    [Fact]
    public void Success_creates_result_with_IsSuccess_true()
    {
        var result = Result<int, TestError>.Success(42);

        result.IsSuccess.Should().BeTrue();
        result.IsFailure.Should().BeFalse();
        result.Value.Should().Be(42);
    }

    [Fact]
    public void Failure_creates_result_with_IsFailure_true()
    {
        var error = new TestError("ERR", "something failed");

        var result = Result<int, TestError>.Failure(error);

        result.IsFailure.Should().BeTrue();
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(error);
    }

    [Fact]
    public void Value_on_failure_throws_InvalidOperationException()
    {
        var result = Result<int, TestError>.Failure(new TestError("ERR", "fail"));

        var act = () => result.Value;

        act.Should().Throw<InvalidOperationException>().WithMessage("*failed result*");
    }

    [Fact]
    public void Error_on_success_throws_InvalidOperationException()
    {
        var result = Result<int, TestError>.Success(42);

        var act = () => result.Error;

        act.Should().Throw<InvalidOperationException>().WithMessage("*successful result*");
    }

    [Fact]
    public void Match_calls_onSuccess_for_successful_result()
    {
        var result = Result<int, TestError>.Success(42);

        var output = result.Match(onSuccess: v => $"ok:{v}", onFailure: e => $"err:{e.Code}");

        output.Should().Be("ok:42");
    }

    [Fact]
    public void Match_calls_onFailure_for_failed_result()
    {
        var result = Result<int, TestError>.Failure(new TestError("NOT_FOUND", "gone"));

        var output = result.Match(onSuccess: v => $"ok:{v}", onFailure: e => $"err:{e.Code}");

        output.Should().Be("err:NOT_FOUND");
    }
}
