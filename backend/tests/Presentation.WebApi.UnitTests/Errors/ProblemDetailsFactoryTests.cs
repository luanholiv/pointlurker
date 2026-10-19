using FluentAssertions;
using Microsoft.AspNetCore.Http;
using PointLurker.Core.Domain.Errors;
using PointLurker.Presentation.WebApi.Errors;

namespace Presentation.WebApi.UnitTests.Errors;

public sealed class ProblemDetailsFactoryTests
{
    [Fact]
    public void Create_maps_domain_error_code_to_problem_details_code()
    {
        var error = new DomainError(ErrorCodes.Users.NotFound);

        var pd = ProblemDetailsFactory.Create(error, StatusCodes.Status404NotFound, "Not Found");

        pd.Extensions.Should().ContainKey("code");
        pd.Extensions["code"].Should().Be(ErrorCodes.Users.NotFound);
    }

    [Fact]
    public void Create_maps_params_to_problem_details_extensions()
    {
        var error = new DomainError(
            ErrorCodes.Stores.AlreadyExists,
            new Dictionary<string, object?> { ["channelId"] = "xqc" }
        );

        var pd = ProblemDetailsFactory.Create(
            error,
            StatusCodes.Status409Conflict,
            "Store already exists"
        );

        pd.Extensions.Should().ContainKey("params");
        var p = pd.Extensions["params"] as IReadOnlyDictionary<string, object?>;
        p.Should().ContainKey("channelId").WhoseValue.Should().Be("xqc");
    }

    [Fact]
    public void Create_sets_status_and_title()
    {
        var error = new DomainError(ErrorCodes.Auth.Unauthorized);

        var pd = ProblemDetailsFactory.Create(
            error,
            StatusCodes.Status401Unauthorized,
            "Unauthorized"
        );

        pd.Status.Should().Be(StatusCodes.Status401Unauthorized);
        pd.Title.Should().Be("Unauthorized");
    }

    [Fact]
    public void Create_sets_type_uri_from_code()
    {
        var error = new DomainError(ErrorCodes.Wishlist.EntryNotFound);

        var pd = ProblemDetailsFactory.Create(error, StatusCodes.Status404NotFound, "Not found");

        pd.Type.Should()
            .Be($"https://errors.app/{ErrorCodes.Wishlist.EntryNotFound.Replace('.', '/')}");
    }

    [Fact]
    public void Create_includes_empty_params_when_domain_error_has_no_params()
    {
        var error = new DomainError(ErrorCodes.System.InternalError);

        var pd = ProblemDetailsFactory.Create(
            error,
            StatusCodes.Status500InternalServerError,
            "Internal error"
        );

        pd.Extensions.Should().ContainKey("params");
        var p = pd.Extensions["params"] as IReadOnlyDictionary<string, object?>;
        p.Should().BeEmpty();
    }
}
