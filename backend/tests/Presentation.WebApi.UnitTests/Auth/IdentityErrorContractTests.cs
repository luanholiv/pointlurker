using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using PointLurker.Core.Domain.Errors;
using PointLurker.Presentation.WebApi.Auth;

namespace Presentation.WebApi.UnitTests.Auth;

/// <summary>
/// The framework Identity endpoints answer failures in their own shape, which carries no stable
/// code — so the only thing a client could render was the English prose. These cases pin what the
/// middleware adds, and just as importantly what it must leave untouched: a success body, a
/// non-problem body, a body that already carries a code, and any path it does not own.
/// </summary>
public sealed class IdentityErrorContractTests
{
    private const string ProblemJson = "application/problem+json";

    [Fact]
    public async Task Validation_failure_gains_the_contract_code_and_mapped_params()
    {
        var response = await RunAsync(
            "/api/auth/register",
            StatusCodes.Status400BadRequest,
            ProblemJson,
            """
            {
              "title": "One or more validation errors occurred.",
              "status": 400,
              "errors": { "PasswordTooShort": ["Passwords must be at least 6 characters."] }
            }
            """
        );

        var body = JsonDocument.Parse(response.Body).RootElement;
        body.GetProperty("code").GetString().Should().Be(ErrorCodes.Validation.Failed);

        var first = body.GetProperty("params").GetProperty("errors")[0];
        first.GetProperty("code").GetString().Should().Be(ErrorCodes.Validation.PasswordTooShort);
        first.GetProperty("field").GetString().Should().Be("password");
        first.GetProperty("params").GetProperty("min").GetInt32().Should().Be(6);
    }

    [Fact]
    public async Task Validation_failure_keeps_the_identity_errors_object()
    {
        // The mapped codes are what the client renders; Identity's own prose stays for whoever is
        // reading a log or a network tab.
        var response = await RunAsync(
            "/api/auth/register",
            StatusCodes.Status400BadRequest,
            ProblemJson,
            """
            { "status": 400, "errors": { "PasswordRequiresDigit": ["Needs a digit."] } }
            """
        );

        var body = JsonDocument.Parse(response.Body).RootElement;
        body.GetProperty("errors").GetProperty("PasswordRequiresDigit").Should().NotBeNull();
    }

    [Fact]
    public async Task Two_factor_challenge_keeps_its_detail_and_gains_a_code()
    {
        // The login challenge is recognised by `detail`, so rewriting that field would break the
        // 2FA prompt. The code is added beside it.
        var response = await RunAsync(
            "/api/auth/login",
            StatusCodes.Status401Unauthorized,
            ProblemJson,
            """
            { "status": 401, "detail": "RequiresTwoFactor" }
            """
        );

        var body = JsonDocument.Parse(response.Body).RootElement;
        body.GetProperty("detail").GetString().Should().Be("RequiresTwoFactor");
        body.GetProperty("code").GetString().Should().Be(ErrorCodes.Auth.TwoFactorRequired);
    }

    [Theory]
    [InlineData("Failed")]
    [InlineData("LockedOut")]
    [InlineData("NotAllowed")]
    public async Task Every_other_sign_in_outcome_collapses_to_unauthorized(string detail)
    {
        // Lockout and not-allowed deliberately share the code: an unauthenticated caller learns
        // only that the attempt failed, never the state of the account.
        var response = await RunAsync(
            "/api/auth/login",
            StatusCodes.Status401Unauthorized,
            ProblemJson,
            $"{{ \"status\": 401, \"detail\": \"{detail}\" }}"
        );

        var body = JsonDocument.Parse(response.Body).RootElement;
        body.GetProperty("code").GetString().Should().Be(ErrorCodes.Auth.Unauthorized);
    }

    /// <summary>
    /// A bad authenticator code and a bad password are the same document — <c>401</c> with
    /// <c>detail: "Failed"</c> — so the response alone cannot tell them apart. The sign-in manager
    /// can: it only attempts a second factor once the password has been accepted, so a rejection it
    /// records is unambiguous.
    /// </summary>
    [Fact]
    public async Task A_rejected_second_factor_is_reported_as_an_invalid_code()
    {
        var response = await RunAsync(
            "/api/auth/login",
            StatusCodes.Status401Unauthorized,
            ProblemJson,
            """
            { "status": 401, "detail": "Failed" }
            """,
            ctx => ctx.Items[TwoFactorOutcomeSignInManager.SecondFactorRejectedKey] = true
        );

        var body = JsonDocument.Parse(response.Body).RootElement;
        body.GetProperty("code").GetString().Should().Be(ErrorCodes.Auth.TwoFactorInvalid);
        body.GetProperty("detail").GetString().Should().Be("Failed");
    }

    /// <summary>
    /// The flag is the signal, not the mere presence of the key: a sign-in that reached the second
    /// factor and passed it leaves the entry unset, and a wrong password never gets that far.
    /// </summary>
    [Fact]
    public async Task An_unflagged_failure_stays_unauthorized()
    {
        var response = await RunAsync(
            "/api/auth/login",
            StatusCodes.Status401Unauthorized,
            ProblemJson,
            """
            { "status": 401, "detail": "Failed" }
            """,
            ctx => ctx.Items["some.unrelated.item"] = true
        );

        var body = JsonDocument.Parse(response.Body).RootElement;
        body.GetProperty("code").GetString().Should().Be(ErrorCodes.Auth.Unauthorized);
    }

    [Fact]
    public async Task Rewritten_body_carries_its_own_content_length()
    {
        // A stale length from the original body would truncate the response mid-JSON.
        var response = await RunAsync(
            "/api/auth/login",
            StatusCodes.Status401Unauthorized,
            ProblemJson,
            """
            { "status": 401, "detail": "Failed" }
            """
        );

        response.ContentLength.Should().Be(response.Body.Length);
    }

    [Fact]
    public async Task Success_body_is_passed_through_unchanged()
    {
        const string token = """{"accessToken":"a","refreshToken":"b","expiresIn":3600}""";

        var response = await RunAsync(
            "/api/auth/login",
            StatusCodes.Status200OK,
            "application/json",
            token
        );

        response.Body.Should().Be(token);
    }

    [Fact]
    public async Task Non_problem_error_body_is_passed_through_unchanged()
    {
        var response = await RunAsync(
            "/api/auth/login",
            StatusCodes.Status502BadGateway,
            "text/plain",
            "upstream said no"
        );

        response.Body.Should().Be("upstream said no");
    }

    [Fact]
    public async Task Server_error_is_passed_through_unchanged()
    {
        // 5xx is the global handler's territory and already contract-shaped.
        const string problem = """{"status":500,"code":"system.internal_error"}""";

        var response = await RunAsync(
            "/api/auth/login",
            StatusCodes.Status500InternalServerError,
            ProblemJson,
            problem
        );

        response.Body.Should().Be(problem);
    }

    [Fact]
    public async Task Body_that_already_carries_a_code_is_left_alone()
    {
        const string problem = """{"status":400,"code":"request.malformed_body"}""";

        var response = await RunAsync(
            "/api/auth/manage/info",
            StatusCodes.Status400BadRequest,
            ProblemJson,
            problem
        );

        response.Body.Should().Be(problem);
    }

    [Fact]
    public async Task A_path_the_middleware_does_not_own_is_passed_through_unchanged()
    {
        const string problem = """{"status":400,"errors":{"PasswordTooShort":["too short"]}}""";

        var response = await RunAsync(
            "/api/stores",
            StatusCodes.Status400BadRequest,
            ProblemJson,
            problem
        );

        response.Body.Should().Be(problem);
    }

    [Fact]
    public async Task An_empty_problem_body_is_passed_through_unchanged()
    {
        var response = await RunAsync(
            "/api/auth/login",
            StatusCodes.Status401Unauthorized,
            ProblemJson,
            string.Empty
        );

        response.Body.Should().BeEmpty();
    }

    /// <summary>
    /// <paramref name="duringRequest" /> runs inside the endpoint, which is where the sign-in
    /// manager records a rejected second factor — seeding it beforehand would prove less.
    /// </summary>
    private static async Task<(int Status, string Body, long? ContentLength)> RunAsync(
        string path,
        int status,
        string contentType,
        string body,
        Action<HttpContext>? duringRequest = null
    )
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;

        using var output = new MemoryStream();
        context.Response.Body = output;

        await IdentityErrorContract.InvokeAsync(
            context,
            async ctx =>
            {
                duringRequest?.Invoke(ctx);
                ctx.Response.StatusCode = status;
                ctx.Response.ContentType = contentType;
                if (body.Length > 0)
                {
                    await ctx.Response.WriteAsync(body);
                }
            }
        );

        output.Position = 0;
        using var reader = new StreamReader(output);
        var written = await reader.ReadToEndAsync();

        return (context.Response.StatusCode, written, context.Response.ContentLength);
    }
}
