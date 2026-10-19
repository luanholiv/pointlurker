using FluentAssertions;
using FluentValidation;
using PointLurker.Core.Domain.Errors;
using PointLurker.Presentation.WebApi.Endpoints.Users.Contracts;
using PointLurker.Presentation.WebApi.Endpoints.Users.Validation;

namespace Presentation.WebApi.UnitTests.Endpoints.Users.Validation;

public class SetCredentialRequestValidatorTests
{
    private readonly SetCredentialRequestValidator _validator = new();

    [Fact]
    public void Accepts_a_populated_secret()
    {
        _validator.Validate(new SetCredentialRequest("jwt-value")).IsValid.Should().BeTrue();
    }

    /// <summary>
    /// The rule names <c>credentials.secret_required</c> rather than the generic
    /// <c>validation.required</c>, so the sentence says a credential is what is missing. A rule may
    /// carry any declared code; validating here is what keeps that sentence without a second guard
    /// in the handler.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Rejects_a_blank_secret_with_the_credential_code(string secret)
    {
        _validator
            .Validate(new SetCredentialRequest(secret))
            .Errors.Should()
            .ContainSingle(e =>
                e.PropertyName == "Secret" && e.ErrorCode == ErrorCodes.Credentials.SecretRequired
            );
    }
}
