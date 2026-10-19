using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using PointLurker.Core.Domain.Errors;
using PointLurker.Presentation.WebApi.Errors;

namespace PointLurker.Presentation.WebApi.Auth;

/// <summary>
/// Brings the <c>MapIdentityApi</c> failure responses under the <c>{code, params}</c> contract every
/// other endpoint already obeys.
/// <para>
/// The framework endpoints answer with the built-in Identity shapes: a <c>ValidationProblem</c>
/// whose <c>errors</c> object is keyed by Identity error code, and a bare <c>401</c> whose
/// <c>detail</c> is the <c>SignInResult</c>. Neither carries a stable code, so the only thing a
/// client could show a user is the English prose the server chose.
/// </para>
/// <para>
/// <c>MapIdentityApi</c> exposes no per-endpoint hook, so this runs as middleware and buffers the
/// response. It <em>augments</em> rather than replaces: every field the framework wrote survives —
/// including the <c>detail</c> the two-factor challenge is recognised by — and <c>code</c> plus
/// <c>params</c> are added beside them.
/// </para>
/// </summary>
public static class IdentityErrorContract
{
    private const string AuthPrefix = "/api/auth";
    private const string ProblemJsonMediaType = "application/problem+json";

    /// <summary>
    /// The <c>MapIdentityApi</c> endpoints. The custom <c>bootstrap</c> endpoint under the same
    /// prefix is deliberately absent: it already emits the contract.
    /// </summary>
    private static readonly string[] FrameworkEndpointSuffixes =
    [
        "/register",
        "/login",
        "/refresh",
        "/manage/2fa",
        "/manage/info",
    ];

    public static IApplicationBuilder UseIdentityErrorContract(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        return app.Use(InvokeAsync);
    }

    internal static async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        if (!OwnsPath(context.Request.Path.Value))
        {
            await next(context);
            return;
        }

        var originalBody = context.Response.Body;
        using var buffer = new MemoryStream();
        context.Response.Body = buffer;

        try
        {
            await next(context);
        }
        finally
        {
            context.Response.Body = originalBody;
        }

        var written = buffer.ToArray();
        var rewritten = ShouldRewrite(context.Response)
            ? TryAddContractExtensions(
                context.Response.StatusCode,
                written,
                SecondFactorWasRejected(context)
            )
            : null;

        var payload = rewritten ?? written;
        // The framework wrote the length of the body it produced; ours is longer.
        context.Response.ContentLength = payload.Length;
        await originalBody.WriteAsync(payload);
    }

    private static bool OwnsPath(string? path)
    {
        if (path is null || !path.StartsWith(AuthPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        foreach (var suffix in FrameworkEndpointSuffixes)
        {
            if (path.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ShouldRewrite(HttpResponse response) =>
        response.StatusCode >= StatusCodes.Status400BadRequest
        && response.StatusCode < StatusCodes.Status500InternalServerError
        && (response.ContentType ?? string.Empty).Contains(
            ProblemJsonMediaType,
            StringComparison.OrdinalIgnoreCase
        );

    /// <summary>
    /// Returns the augmented body, or <see langword="null" /> when the payload is not one this
    /// middleware recognises and must therefore be left exactly as written.
    /// </summary>
    private static byte[]? TryAddContractExtensions(
        int statusCode,
        byte[] body,
        bool secondFactorRejected
    )
    {
        if (body.Length == 0)
        {
            return null;
        }

        JsonObject? problem;
        try
        {
            problem = JsonNode.Parse(body) as JsonObject;
        }
        catch (JsonException)
        {
            return null;
        }

        if (problem is null || problem.ContainsKey("code"))
        {
            return null;
        }

        if (problem["errors"] is JsonObject identityErrors)
        {
            AddValidationContract(problem, identityErrors);
        }
        else if (statusCode == StatusCodes.Status401Unauthorized)
        {
            AddSignInContract(problem, secondFactorRejected);
        }
        else
        {
            return null;
        }

        return Encoding.UTF8.GetBytes(problem.ToJsonString());
    }

    private static void AddValidationContract(JsonObject problem, JsonObject identityErrors)
    {
        var byCode = identityErrors.ToDictionary(
            pair => pair.Key,
            pair => Descriptions(pair.Value),
            StringComparer.Ordinal
        );

        var mapped = IdentityErrorMapper.ToValidationProblemDetails(byCode);

        problem["code"] = JsonValue.Create((string?)mapped.Extensions["code"]);
        problem["params"] = JsonSerializer.SerializeToNode(
            mapped.Extensions["params"],
            JsonSerializerOptions.Web
        );
    }

    /// <summary>
    /// The <c>detail</c> of an Identity <c>401</c> is <c>SignInResult.ToString()</c>, which tells the
    /// two-factor challenge apart and nothing else: a refused authenticator code reads <c>Failed</c>,
    /// exactly like a wrong password, so that distinction comes from
    /// <see cref="TwoFactorOutcomeSignInManager" />. <c>LockedOut</c> and <c>NotAllowed</c> join
    /// <c>Failed</c> so an unauthenticated caller learns nothing about the account's state.
    /// </summary>
    private static void AddSignInContract(JsonObject problem, bool secondFactorRejected)
    {
        var detail = problem["detail"]?.GetValue<string>();
        var code = detail switch
        {
            "RequiresTwoFactor" => ErrorCodes.Auth.TwoFactorRequired,
            _ when secondFactorRejected => ErrorCodes.Auth.TwoFactorInvalid,
            _ => ErrorCodes.Auth.Unauthorized,
        };

        problem["code"] = JsonValue.Create(code);
        problem["params"] = new JsonObject();
    }

    private static bool SecondFactorWasRejected(HttpContext context) =>
        context.Items.TryGetValue(
            TwoFactorOutcomeSignInManager.SecondFactorRejectedKey,
            out var flag
        ) && flag is true;

    private static string[] Descriptions(JsonNode? value) =>
        value is JsonArray array
            ? [.. array.Select(entry => entry?.GetValue<string>() ?? string.Empty)]
            : [value?.GetValue<string>() ?? string.Empty];
}
