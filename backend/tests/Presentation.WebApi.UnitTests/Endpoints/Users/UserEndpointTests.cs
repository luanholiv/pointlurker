using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using PointLurker.Core.Application.Identity;
using PointLurker.Core.Domain.Errors;
using PointLurker.Core.Domain.Identity;
using PointLurker.Core.Domain.Localization;
using PointLurker.Core.Domain.Stores.Enums;
using PointLurker.Presentation.WebApi.Endpoints.Users;
using PointLurker.Presentation.WebApi.Endpoints.Users.Contracts;

namespace Presentation.WebApi.UnitTests.Endpoints.Users;

public sealed class UserEndpointTests
{
    private readonly ISingleUserProvider _userProvider = Substitute.For<ISingleUserProvider>();
    private readonly IProviderCredentialProvider _credentials =
        Substitute.For<IProviderCredentialProvider>();

    [Fact]
    public async Task GetCurrentUser_returns_profile_without_roles()
    {
        var user = CreateUser();
        _userProvider.GetAsync(Arg.Any<CancellationToken>()).Returns(user);

        var result = await UserEndpoints.GetCurrentUserAsync(
            _userProvider,
            _credentials,
            CancellationToken.None
        );

        var value = result.Should().BeAssignableTo<IValueHttpResult>().Subject.Value;
        // The response type intentionally has no Roles member.
        value!.GetType().GetProperty("Roles").Should().BeNull();
        value.GetType().GetProperty("Email")!.GetValue(value).Should().Be(user.Email);
    }

    [Fact]
    public async Task GetCurrentUser_lists_the_StreamElements_credential()
    {
        var user = CreateUser();
        _userProvider.GetAsync(Arg.Any<CancellationToken>()).Returns(user);
        _credentials
            .ListAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns([
                new ProviderCredential(
                    user.Id,
                    StoreProvider.StreamElements,
                    "ciphertext",
                    DateTimeOffset.UtcNow
                ),
            ]);

        var result = await UserEndpoints.GetCurrentUserAsync(
            _userProvider,
            _credentials,
            CancellationToken.None
        );

        var value = (CurrentUserResponse)
            result.Should().BeAssignableTo<IValueHttpResult>().Subject.Value!;
        value.Credentials.Should().ContainSingle().Which.Provider.Should().Be("streamelements");
    }

    [Fact]
    public async Task GetCurrentUser_returns_404_when_the_owner_row_is_missing()
    {
        _userProvider.GetAsync(Arg.Any<CancellationToken>()).Returns((ApplicationUser?)null);

        var result = await UserEndpoints.GetCurrentUserAsync(
            _userProvider,
            _credentials,
            CancellationToken.None
        );

        StatusCodeOf(result).Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task SetLocale_returns_204_and_persists_for_a_supported_locale()
    {
        var user = CreateUser();
        _userProvider.GetAsync(Arg.Any<CancellationToken>()).Returns(user);

        var result = await UserEndpoints.SetLocaleAsync(
            new SetLocaleRequest(SupportedLocales.PtBr),
            new SetUserLocale(_userProvider),
            CancellationToken.None
        );

        StatusCodeOf(result).Should().Be(StatusCodes.Status204NoContent);
        user.Locale.Should().Be(SupportedLocales.PtBr);
        await _userProvider.Received(1).SaveAsync(user, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetLocale_returns_400_with_code_and_params_for_an_unsupported_locale()
    {
        var result = await UserEndpoints.SetLocaleAsync(
            new SetLocaleRequest("xx-XX"),
            new SetUserLocale(_userProvider),
            CancellationToken.None
        );

        StatusCodeOf(result).Should().Be(StatusCodes.Status400BadRequest);

        var problem = (ProblemDetails)
            result.Should().BeAssignableTo<IValueHttpResult>().Subject.Value!;
        problem.Extensions["code"].Should().Be(ErrorCodes.Users.LocaleUnsupported);
        problem.Title.Should().Be("Locale 'xx-XX' is not supported.");

        await _userProvider
            .DidNotReceive()
            .SaveAsync(Arg.Any<ApplicationUser>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetLocale_does_not_read_the_owner_before_validating()
    {
        // Validation ordering is contractual: the 400 must cost zero database work.
        await UserEndpoints.SetLocaleAsync(
            new SetLocaleRequest("xx-XX"),
            new SetUserLocale(_userProvider),
            CancellationToken.None
        );

        await _userProvider.DidNotReceive().GetAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ClearCredential_returns_204_and_reads_the_owner_exactly_once()
    {
        var user = CreateUser();
        _userProvider.GetAsync(Arg.Any<CancellationToken>()).Returns(user);

        var result = await UserEndpoints.ClearCredentialAsync(
            new CredentialProviderRoute("streamelements"),
            new ClearProviderCredential(_userProvider, _credentials),
            CancellationToken.None
        );

        StatusCodeOf(result).Should().Be(StatusCodes.Status204NoContent);
        // Exactly one read, made by the use case — the handler does not pre-fetch.
        await _userProvider.Received(1).GetAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("nope")]
    [InlineData("0")]
    [InlineData("StreamElements")]
    public async Task ClearCredential_answers_404_with_the_catalog_code_for_an_unknown_provider(
        string provider
    )
    {
        var result = await UserEndpoints.ClearCredentialAsync(
            new CredentialProviderRoute(provider),
            new ClearProviderCredential(_userProvider, _credentials),
            CancellationToken.None
        );

        StatusCodeOf(result).Should().Be(StatusCodes.Status404NotFound);
        var problem = (ProblemDetails)
            result.Should().BeAssignableTo<IValueHttpResult>().Subject.Value!;
        problem.Extensions["code"].Should().Be(ErrorCodes.Credentials.ProviderUnsupported);
        await _userProvider.DidNotReceive().GetAsync(Arg.Any<CancellationToken>());
    }

    private static int? StatusCodeOf(IResult result) =>
        result.Should().BeAssignableTo<IStatusCodeHttpResult>().Subject.StatusCode;

    private static ApplicationUser CreateUser() =>
        new()
        {
            Id = Guid.NewGuid(),
            UserName = "test@test.com",
            Email = "test@test.com",
        };
}
