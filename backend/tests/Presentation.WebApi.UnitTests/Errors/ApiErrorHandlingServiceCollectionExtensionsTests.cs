using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PointLurker.Presentation.WebApi.Errors;

namespace Presentation.WebApi.UnitTests.Errors;

public class ApiErrorHandlingServiceCollectionExtensionsTests
{
    [Fact]
    public void Registers_the_global_exception_handler()
    {
        var services = new ServiceCollection().AddApiErrorHandling();

        services.Should().Contain(d => d.ImplementationType == typeof(GlobalExceptionHandler));
    }

    [Fact]
    public void Registers_problem_details_after_the_exception_handler()
    {
        var services = new ServiceCollection().AddApiErrorHandling().ToList();

        var handlerIndex = services.FindIndex(d =>
            d.ImplementationType == typeof(GlobalExceptionHandler)
        );
        var problemDetailsIndex = services.FindIndex(d =>
            d.ServiceType == typeof(IProblemDetailsService)
        );

        handlerIndex.Should().BeGreaterThanOrEqualTo(0);
        problemDetailsIndex.Should().BeGreaterThan(handlerIndex);
    }

    /// <summary>
    /// <c>InvalidModelStateResponseFactory</c> is an MVC hook: it fires for controller actions, and
    /// this application is minimal-API throughout. Configuring it produced code that read as wired
    /// and never ran. Per-field validation now travels through an endpoint filter instead, so this
    /// asserts the unreachable hook stays gone rather than being re-added by reflex.
    /// </summary>
    [Fact]
    public void Does_not_configure_the_unreachable_mvc_model_state_hook()
    {
        var services = new ServiceCollection().AddApiErrorHandling();

        services
            .Should()
            .NotContain(d => d.ServiceType == typeof(IConfigureOptions<ApiBehaviorOptions>));
    }
}
