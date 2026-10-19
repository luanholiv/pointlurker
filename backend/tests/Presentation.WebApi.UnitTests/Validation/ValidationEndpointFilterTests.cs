using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using PointLurker.Core.Domain.Errors;
using PointLurker.Presentation.WebApi.Endpoints.Users.Contracts;
using PointLurker.Presentation.WebApi.Endpoints.Users.Validation;
using PointLurker.Presentation.WebApi.Validation;

namespace Presentation.WebApi.UnitTests.Validation;

/// <summary>
/// The seam that makes per-field validation reachable at all: the mapper it delegates to cannot be
/// reached from an MVC-only hook, so a filter that runs is the whole point. These tests pin that the
/// endpoint is reached on success and never reached on failure.
/// </summary>
public class ValidationEndpointFilterTests
{
    [Fact]
    public async Task Invokes_the_endpoint_when_the_request_is_valid()
    {
        var (context, next) = Invocation(new SetLocaleRequest("en-US"));

        var result = await Filter().InvokeAsync(context, next.Delegate);

        next.WasCalled.Should().BeTrue();
        result.Should().BeOfType<NoContent>();
    }

    [Fact]
    public async Task Does_not_invoke_the_endpoint_when_the_request_is_invalid()
    {
        var (context, next) = Invocation(new SetLocaleRequest(""));

        await Filter().InvokeAsync(context, next.Delegate);

        next.WasCalled.Should().BeFalse();
    }

    [Fact]
    public async Task Answers_an_invalid_request_with_the_validation_failed_problem()
    {
        var (context, next) = Invocation(new SetLocaleRequest(""));

        var result = await Filter().InvokeAsync(context, next.Delegate);

        var problem = result.Should().BeOfType<ProblemHttpResult>().Subject;
        problem.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        problem.ProblemDetails.Extensions["code"].Should().Be(ErrorCodes.Validation.Failed);
    }

    /// <summary>
    /// A handler may take the validated contract alongside route or service parameters, and a
    /// filter registered for a type the invocation does not carry must stay out of the way rather
    /// than reject the call.
    /// </summary>
    [Fact]
    public async Task Passes_through_when_the_invocation_carries_no_such_argument()
    {
        var context = EndpointFilterInvocationContext.Create(
            new DefaultHttpContext(),
            Guid.NewGuid()
        );
        var next = new SpyNext();

        var result = await Filter().InvokeAsync(context, next.Delegate);

        next.WasCalled.Should().BeTrue();
        result.Should().BeOfType<NoContent>();
    }

    /// <summary>
    /// A validator may hit the database or an upstream service, so a client that disconnects must
    /// cancel it rather than leave it running.
    /// </summary>
    [Fact]
    public async Task Hands_the_request_cancellation_token_to_the_validator()
    {
        using var cts = new CancellationTokenSource();
        var httpContext = new DefaultHttpContext { RequestAborted = cts.Token };
        var context = EndpointFilterInvocationContext.Create(
            httpContext,
            new SetLocaleRequest("en-US")
        );
        var validator = new TokenRecordingValidator();

        await new ValidationEndpointFilter<SetLocaleRequest>(validator).InvokeAsync(
            context,
            new SpyNext().Delegate
        );

        validator.ReceivedToken.Should().Be(cts.Token);
    }

    private static ValidationEndpointFilter<SetLocaleRequest> Filter() =>
        new(new SetLocaleRequestValidator());

    private static (EndpointFilterInvocationContext Context, SpyNext Next) Invocation(
        SetLocaleRequest request
    ) => (EndpointFilterInvocationContext.Create(new DefaultHttpContext(), request), new SpyNext());

    private sealed class SpyNext
    {
        public bool WasCalled { get; private set; }

        public EndpointFilterDelegate Delegate =>
            _ =>
            {
                WasCalled = true;
                return ValueTask.FromResult<object?>(TypedResults.NoContent());
            };
    }

    /// <summary>
    /// A real validator rather than a substitute: the contracts are <c>internal</c> and
    /// FluentValidation is strong-named, so Castle cannot proxy <c>IValidator&lt;TRequest&gt;</c>
    /// without widening this assembly's internals to the proxy generator. Subclassing also
    /// exercises the real extension-to-interface routing the filter depends on.
    /// </summary>
    private sealed class TokenRecordingValidator : AbstractValidator<SetLocaleRequest>
    {
        public CancellationToken ReceivedToken { get; private set; }

        public override Task<ValidationResult> ValidateAsync(
            ValidationContext<SetLocaleRequest> context,
            CancellationToken cancellation = default
        )
        {
            ReceivedToken = cancellation;
            return base.ValidateAsync(context, cancellation);
        }
    }
}
