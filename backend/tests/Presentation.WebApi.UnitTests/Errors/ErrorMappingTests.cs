using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using PointLurker.Core.Application.Errors;
using PointLurker.Core.Application.Identity;
using PointLurker.Core.Application.Stores;
using PointLurker.Core.Domain.Errors;
using PointLurker.Core.Domain.Stores;
using PointLurker.Presentation.WebApi.Errors;

namespace Presentation.WebApi.UnitTests.Errors;

public sealed class ErrorMappingTests
{
    [Fact]
    public void StoreError_NotFound_maps_to_404_with_the_canonical_code()
    {
        var problem = Problem(StoreError.NotFound());

        problem.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        Code(problem).Should().Be(ErrorCodes.Stores.NotFound);
    }

    [Fact]
    public void StoreError_AlreadyExists_maps_to_409_and_carries_the_channel_id_param()
    {
        var problem = Problem(StoreError.AlreadyExists("ch1"));

        problem.StatusCode.Should().Be(StatusCodes.Status409Conflict);
        Code(problem).Should().Be(ErrorCodes.Stores.AlreadyExists);
        Params(problem)["channelId"].Should().Be("ch1");
    }

    [Fact]
    public void WishlistError_EntryNotFound_maps_to_404_with_the_canonical_code()
    {
        var problem = Problem(WishlistError.EntryNotFound());

        problem.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        Code(problem).Should().Be(ErrorCodes.Wishlist.EntryNotFound);
    }

    [Fact]
    public void WishlistError_ItemNotFound_maps_to_404_with_the_canonical_code()
    {
        var problem = Problem(WishlistError.ItemNotFound());

        problem.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        Code(problem).Should().Be(ErrorCodes.Wishlist.ItemNotFound);
    }

    [Fact]
    public void WishlistError_AlreadyWishlisted_maps_to_409_with_the_canonical_code()
    {
        var problem = Problem(WishlistError.AlreadyWishlisted());

        problem.StatusCode.Should().Be(StatusCodes.Status409Conflict);
        Code(problem).Should().Be(ErrorCodes.Wishlist.AlreadyExists);
    }

    [Fact]
    public void WishlistError_MissingInputCoverage_maps_to_422_and_carries_the_missing_labels()
    {
        var problem = Problem(WishlistError.MissingInputCoverage(["email"]));

        problem.StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);
        Code(problem).Should().Be(ErrorCodes.Wishlist.MissingInputCoverage);
        Params(problem)["missingLabels"]
            .Should()
            .BeAssignableTo<IReadOnlyList<string>>()
            .Which.Should()
            .Equal("email");
    }

    [Fact]
    public void IdentityError_UserNotFound_maps_to_404_with_the_canonical_code()
    {
        var problem = Problem(IdentityError.UserNotFound());

        problem.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        Code(problem).Should().Be(ErrorCodes.Users.NotFound);
    }

    [Fact]
    public void An_error_without_params_still_exposes_an_empty_params_object()
    {
        var problem = Problem(StoreError.NotFound());

        Params(problem).Should().BeEmpty();
    }

    [Fact]
    public void The_title_is_the_developer_facing_English_message()
    {
        var problem = Problem(StoreError.AlreadyExists("ch1"));

        problem.ProblemDetails.Title.Should().Be("A store with channel ID 'ch1' already exists.");
    }

    [Fact]
    public void The_type_uri_is_derived_from_the_code()
    {
        var problem = Problem(WishlistError.EntryNotFound());

        problem.ProblemDetails.Type.Should().Be("https://errors.app/wishlist/entry_not_found");
    }

    private static ProblemHttpResult Problem<TError>(TError error)
        where TError : IApplicationError =>
        error.ToHttpResult().Should().BeOfType<ProblemHttpResult>().Subject;

    private static object? Code(ProblemHttpResult problem) =>
        problem.ProblemDetails.Extensions["code"];

    private static IReadOnlyDictionary<string, object?> Params(ProblemHttpResult problem) =>
        problem
            .ProblemDetails.Extensions["params"]
            .Should()
            .BeAssignableTo<IReadOnlyDictionary<string, object?>>()
            .Subject;
}
