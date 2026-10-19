using System.Reflection;
using System.Text.RegularExpressions;
using FluentAssertions;
using PointLurker.Core.Domain.Errors;

namespace Core.Domain.UnitTests.Errors;

public sealed class ErrorCodesTests
{
    private static readonly Regex CodePattern = new(
        @"^[a-z][a-z0-9_]*(\.[a-z][a-z0-9_]*)+$",
        RegexOptions.Compiled
    );

    [Fact]
    public void All_error_codes_match_naming_convention()
    {
        var codes = GetAllCodes();

        codes.Should().NotBeEmpty();

        foreach (var code in codes)
        {
            CodePattern
                .IsMatch(code)
                .Should()
                .BeTrue(
                    $"code '{code}' must match ^[a-z][a-z0-9_]*(\\.[a-z][a-z0-9_]*)+$ — at least two dot-separated snake_case segments"
                );
        }
    }

    [Fact]
    public void All_error_codes_are_unique()
    {
        var codes = GetAllCodes();

        codes.Should().OnlyHaveUniqueItems("duplicate error codes would cause silent collisions");
    }

    [Fact]
    public void Error_codes_have_at_least_two_segments()
    {
        var codes = GetAllCodes();

        foreach (var code in codes)
        {
            code.Split('.')
                .Should()
                .HaveCountGreaterThanOrEqualTo(2, $"code '{code}' must have at least two segments");
        }
    }

    [Fact]
    public void Validation_namespace_codes_exist()
    {
        ErrorCodes.Validation.Failed.Should().Be("validation.failed");
        ErrorCodes.Validation.Required.Should().Be("validation.required");
        ErrorCodes.Validation.TooLong.Should().Be("validation.too_long");
        ErrorCodes.Validation.TooShort.Should().Be("validation.too_short");
        ErrorCodes.Validation.InvalidFormat.Should().Be("validation.invalid_format");
    }

    [Fact]
    public void Validation_password_codes_exist()
    {
        ErrorCodes.Validation.PasswordTooShort.Should().Be("validation.password_too_short");
        ErrorCodes
            .Validation.PasswordRequiresDigit.Should()
            .Be("validation.password_requires_digit");
        ErrorCodes
            .Validation.PasswordRequiresUpper.Should()
            .Be("validation.password_requires_upper");
        ErrorCodes
            .Validation.PasswordRequiresLower.Should()
            .Be("validation.password_requires_lower");
        ErrorCodes
            .Validation.PasswordRequiresNonAlphanumeric.Should()
            .Be("validation.password_requires_non_alphanumeric");
        ErrorCodes
            .Validation.PasswordRequiresUniqueChars.Should()
            .Be("validation.password_requires_unique_chars");
        ErrorCodes.Validation.PasswordMismatch.Should().Be("validation.password_mismatch");
        ErrorCodes.Validation.DuplicateUserName.Should().Be("validation.duplicate_user_name");
        ErrorCodes.Validation.InvalidUserName.Should().Be("validation.invalid_user_name");
        ErrorCodes.Validation.DuplicateEmail.Should().Be("validation.duplicate_email");
        ErrorCodes.Validation.InvalidEmail.Should().Be("validation.invalid_email");
    }

    [Fact]
    public void Request_namespace_codes_exist()
    {
        ErrorCodes.Request.MalformedBody.Should().Be("request.malformed_body");
        ErrorCodes.Request.BodyTooLarge.Should().Be("request.body_too_large");
    }

    [Fact]
    public void Users_namespace_codes_exist()
    {
        ErrorCodes.Users.RegisterAlreadyExists.Should().Be("users.register_already_exists");
        ErrorCodes.Users.NotFound.Should().Be("users.not_found");
        ErrorCodes.Users.LocaleUnsupported.Should().Be("users.locale_unsupported");
    }

    [Fact]
    public void Auth_namespace_codes_exist()
    {
        ErrorCodes.Auth.Unauthorized.Should().Be("auth.unauthorized");
        ErrorCodes.Auth.TwoFactorRequired.Should().Be("auth.two_factor_required");
        ErrorCodes.Auth.TwoFactorInvalid.Should().Be("auth.two_factor_invalid");
        ErrorCodes
            .Auth.ProviderCredentialNotConfigured.Should()
            .Be("auth.provider_credential_not_configured");
    }

    [Fact]
    public void Credentials_namespace_codes_exist()
    {
        ErrorCodes.Credentials.SecretRequired.Should().Be("credentials.secret_required");
        ErrorCodes.Credentials.Rejected.Should().Be("credentials.rejected");
        ErrorCodes.Credentials.ProviderUnsupported.Should().Be("credentials.provider_unsupported");
    }

    [Fact]
    public void Stores_namespace_codes_exist()
    {
        ErrorCodes.Stores.NotFound.Should().Be("stores.not_found");
        ErrorCodes.Stores.AlreadyExists.Should().Be("stores.already_exists");
        ErrorCodes.Stores.ProviderUnsupported.Should().Be("stores.provider_unsupported");
        ErrorCodes.Stores.IdentityNotAccepted.Should().Be("stores.identity_not_accepted");
    }

    [Fact]
    public void Wishlist_namespace_codes_exist()
    {
        ErrorCodes.Wishlist.EntryNotFound.Should().Be("wishlist.entry_not_found");
        ErrorCodes.Wishlist.ItemNotFound.Should().Be("wishlist.item_not_found");
        ErrorCodes.Wishlist.AlreadyExists.Should().Be("wishlist.already_exists");
        ErrorCodes.Wishlist.MissingInputCoverage.Should().Be("wishlist.missing_input_coverage");
        ErrorCodes.Wishlist.MessagesNotSupported.Should().Be("wishlist.messages_not_supported");
        ErrorCodes.Credentials.Expired.Should().Be("credentials.expired");
        ErrorCodes
            .RedemptionFailure.ProviderRejected.Should()
            .Be("redemption.failure.provider_rejected");
        ErrorCodes
            .RedemptionFailure.CredentialExpired.Should()
            .Be("redemption.failure.credential_expired");
    }

    [Fact]
    public void StoreLookup_namespace_codes_exist()
    {
        ErrorCodes.StoreLookup.QueryRequired.Should().Be("store_lookup.query_required");
        ErrorCodes.StoreLookup.NotFound.Should().Be("store_lookup.not_found");
    }

    [Fact]
    public void Search_namespace_codes_exist()
    {
        ErrorCodes.Search.InvalidEnumValue.Should().Be("search.invalid_enum_value");
        ErrorCodes.Search.InvalidFilterValue.Should().Be("search.invalid_filter_value");
    }

    [Fact]
    public void ProviderApi_namespace_codes_exist()
    {
        ErrorCodes.ProviderApi.Unreachable.Should().Be("provider_api.unreachable");
        ErrorCodes.ProviderApi.FetchFailed.Should().Be("provider_api.fetch_failed");
    }

    [Fact]
    public void RedemptionFailure_namespace_codes_exist()
    {
        ErrorCodes.RedemptionFailure.Unknown.Should().Be("redemption.failure.unknown");
        ErrorCodes
            .RedemptionFailure.TokenDecryptionFailed.Should()
            .Be("redemption.failure.token_decryption_failed");
        ErrorCodes
            .RedemptionFailure.InputCoverageLost.Should()
            .Be("redemption.failure.input_coverage_lost");
        ErrorCodes
            .RedemptionFailure.InputDecryptionFailed.Should()
            .Be("redemption.failure.input_decryption_failed");
        ErrorCodes
            .RedemptionFailure.ProviderApiBadRequest.Should()
            .Be("redemption.failure.provider_api_bad_request");
        ErrorCodes
            .RedemptionFailure.ProviderApiUnauthorized.Should()
            .Be("redemption.failure.provider_api_unauthorized");
        ErrorCodes
            .RedemptionFailure.ProviderApiForbidden.Should()
            .Be("redemption.failure.provider_api_forbidden");
        ErrorCodes
            .RedemptionFailure.ProviderApiNotFound.Should()
            .Be("redemption.failure.provider_api_not_found");
        ErrorCodes
            .RedemptionFailure.ProviderApiServerError.Should()
            .Be("redemption.failure.provider_api_server_error");
        ErrorCodes
            .RedemptionFailure.InsufficientPoints.Should()
            .Be("redemption.failure.insufficient_points");
    }

    [Fact]
    public void System_namespace_codes_exist()
    {
        ErrorCodes.System.InternalError.Should().Be("system.internal_error");
        ErrorCodes.System.UpstreamUnavailable.Should().Be("system.upstream_unavailable");
    }

    private static IReadOnlyList<string> GetAllCodes()
    {
        // Walk all nested static classes in ErrorCodes and collect string constants
        return CollectCodesFromType(typeof(ErrorCodes));

        static List<string> CollectCodesFromType(Type type)
        {
            var codes = new List<string>();

            foreach (
                var field in type.GetFields(
                    BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy
                )
            )
            {
                if (field.FieldType == typeof(string) && field.GetValue(null) is string value)
                    codes.Add(value);
            }

            foreach (var nested in type.GetNestedTypes(BindingFlags.Public | BindingFlags.Static))
                codes.AddRange(CollectCodesFromType(nested));

            return codes;
        }
    }
}
