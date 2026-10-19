using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.BearerToken;

namespace IntegrationTests.Auth;

// Two-factor management is now the framework endpoint POST /api/auth/manage/2fa. A 2FA-required
// login surfaces the built-in ProblemDetails with detail == "RequiresTwoFactor".
[Collection(ContainersCollection.Name)]
public sealed class TwoFactorFlowTests : IAsyncLifetime, IDisposable
{
    private const string Password = "Password123!";
    private readonly DatabaseFixture _fixture;
    private TestWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;
    private string _email = null!;

    public TwoFactorFlowTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        await _fixture.ResetAsync();
        _factory = new TestWebApplicationFactory(_fixture.ConnectionString);
        _client = _factory.CreateClient();
        _email = $"admin-{Guid.NewGuid():N}@example.com";
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose()
    {
        _client?.Dispose();
        _factory?.Dispose();
    }

    [Fact]
    public async Task Enable_2fa_then_login_requires_and_accepts_a_code()
    {
        var (_, sharedKey) = await EnrolTwoFactorAsync();

        // Step 3: login without a code -> 401 RequiresTwoFactor.
        var loginWithoutCode = await _client.PostAsJsonAsync(
            "/api/auth/login",
            new { email = _email, password = Password }
        );
        Assert.Equal(HttpStatusCode.Unauthorized, loginWithoutCode.StatusCode);
        var challenge = await loginWithoutCode.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("RequiresTwoFactor", challenge.GetProperty("detail").GetString());
        Assert.Equal("auth.two_factor_required", challenge.GetProperty("code").GetString());

        // Step 4: login with a code -> 200 AccessTokenResponse.
        var loginWithCode = await _client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                email = _email,
                password = Password,
                twoFactorCode = GenerateTotp(sharedKey),
            }
        );
        Assert.True(
            loginWithCode.IsSuccessStatusCode,
            $"login with code failed: {loginWithCode.StatusCode} {await loginWithCode.Content.ReadAsStringAsync()}"
        );
        var tokenResponse = await loginWithCode.Content.ReadFromJsonAsync<AccessTokenResponse>();
        Assert.False(string.IsNullOrWhiteSpace(tokenResponse!.AccessToken));
    }

    // These cases assert the wire code rather than the ErrorCodes constant on purpose, as the
    // sibling contract tests do: the mapping is pinned to the strings ASP.NET Identity actually
    // emits, and a framework bump is free to change them.

    /// <summary>
    /// The enrolment endpoint says which half failed, so this needs nothing but the mapping. The
    /// entry is nested in <c>validation.failed</c> because Identity answers with a
    /// <c>ValidationProblem</c>, and it anchors to the input the wrong code was typed into.
    /// </summary>
    [Fact]
    public async Task A_wrong_code_when_enabling_2fa_reports_an_invalid_two_factor_code()
    {
        await RegisterAsync();
        var token = await LoginAsync();
        var sharedKey = await FetchSharedKeyAsync(token);

        var response = await AuthedPostAsJsonAsync(
            "/api/auth/manage/2fa",
            new { enable = true, twoFactorCode = WrongTotp(sharedKey) },
            token
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("validation.failed", body.GetProperty("code").GetString());
        var entry = body.GetProperty("params").GetProperty("errors")[0];
        Assert.Equal("auth.two_factor_invalid", entry.GetProperty("code").GetString());
        Assert.Equal("twoFactorCode", entry.GetProperty("field").GetString());
    }

    [Fact]
    public async Task Enabling_2fa_with_no_code_reports_the_code_as_required()
    {
        await RegisterAsync();
        var token = await LoginAsync();
        await FetchSharedKeyAsync(token);

        var response = await AuthedPostAsJsonAsync(
            "/api/auth/manage/2fa",
            new { enable = true },
            token
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var entry = body.GetProperty("params").GetProperty("errors")[0];
        Assert.Equal("auth.two_factor_required", entry.GetProperty("code").GetString());
    }

    /// <summary>
    /// The login challenge is the case the response cannot decide on its own: a rejected
    /// authenticator code is a <c>401</c> with <c>detail: "Failed"</c>, exactly like a wrong
    /// password. The sign-in manager is what tells them apart.
    /// </summary>
    [Fact]
    public async Task A_wrong_code_at_the_login_challenge_reports_an_invalid_two_factor_code()
    {
        var (_, sharedKey) = await EnrolTwoFactorAsync();

        var response = await _client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                email = _email,
                password = Password,
                twoFactorCode = WrongTotp(sharedKey),
            }
        );

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("auth.two_factor_invalid", body.GetProperty("code").GetString());
        Assert.Equal("Failed", body.GetProperty("detail").GetString());
    }

    /// <summary>
    /// The code here is the correct one, and the password is not. Identity only attempts a second
    /// factor once the password has been accepted, so this attempt never reaches one — and must not
    /// be reported as a bad code. Deciding from the request body alone would get this wrong.
    /// </summary>
    [Fact]
    public async Task A_wrong_password_carrying_a_valid_code_still_reports_unauthorized()
    {
        var (_, sharedKey) = await EnrolTwoFactorAsync();

        var response = await _client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                email = _email,
                password = "WrongPassword123!",
                twoFactorCode = GenerateTotp(sharedKey),
            }
        );

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("auth.unauthorized", body.GetProperty("code").GetString());
    }

    /// <summary>
    /// Disable is what the settings page sends when the owner turns two-factor off. It asks for a
    /// shared-key reset alongside, so the enrolled authenticator stops working: turning 2FA off
    /// has to revoke the secret, or an owner who disables *because* the secret leaked still has a
    /// live secret afterwards.
    /// </summary>
    [Fact]
    public async Task Disabling_2fa_drops_the_login_challenge_and_rotates_the_shared_key()
    {
        var (token, sharedKey) = await EnrolTwoFactorAsync();

        var disableResponse = await AuthedPostAsJsonAsync(
            "/api/auth/manage/2fa",
            new { enable = false, resetSharedKey = true },
            token
        );
        Assert.True(
            disableResponse.IsSuccessStatusCode,
            $"manage/2fa disable failed: {disableResponse.StatusCode} {await disableResponse.Content.ReadAsStringAsync()}"
        );

        var disabled = await disableResponse.Content.ReadFromJsonAsync<TwoFactorResponse>();
        Assert.False(disabled!.IsTwoFactorEnabled);
        Assert.NotEqual(sharedKey, disabled.SharedKey);

        // The password alone signs in again — there is no second factor left to answer.
        var login = await _client.PostAsJsonAsync(
            "/api/auth/login",
            new { email = _email, password = Password }
        );
        Assert.True(
            login.IsSuccessStatusCode,
            $"login after disable failed: {login.StatusCode} {await login.Content.ReadAsStringAsync()}"
        );
        var tokenResponse = await login.Content.ReadFromJsonAsync<AccessTokenResponse>();
        Assert.False(string.IsNullOrWhiteSpace(tokenResponse!.AccessToken));
    }

    /// <summary>
    /// The settings page has no other way to learn that the account already has an authenticator:
    /// the framework <c>GET manage/info</c> is suppressed and would not carry the flag anyway, and
    /// reading it off <c>POST manage/2fa</c> would hand out the shared key on every page load.
    /// </summary>
    [Fact]
    public async Task The_current_user_resource_reports_whether_two_factor_is_enabled()
    {
        await RegisterAsync();
        var token = await LoginAsync();

        var before = await AuthedGetJsonAsync("/api/users/me", token);
        Assert.False(before.GetProperty("twoFactorEnabled").GetBoolean());

        var sharedKey = await FetchSharedKeyAsync(token);
        var enableResponse = await AuthedPostAsJsonAsync(
            "/api/auth/manage/2fa",
            new { enable = true, twoFactorCode = GenerateTotp(sharedKey) },
            token
        );
        Assert.True(enableResponse.IsSuccessStatusCode);

        var after = await AuthedGetJsonAsync("/api/users/me", token);
        Assert.True(after.GetProperty("twoFactorEnabled").GetBoolean());
    }

    /// <summary>
    /// Registers, signs in, and enables 2FA, returning the bearer token and the shared key.
    /// </summary>
    private async Task<(string Token, string SharedKey)> EnrolTwoFactorAsync()
    {
        await RegisterAsync();
        var token = await LoginAsync();
        var sharedKey = await FetchSharedKeyAsync(token);

        var enableResponse = await AuthedPostAsJsonAsync(
            "/api/auth/manage/2fa",
            new { enable = true, twoFactorCode = GenerateTotp(sharedKey) },
            token
        );
        Assert.True(
            enableResponse.IsSuccessStatusCode,
            $"manage/2fa enable failed: {enableResponse.StatusCode} {await enableResponse.Content.ReadAsStringAsync()}"
        );
        var enabledPayload = await enableResponse.Content.ReadFromJsonAsync<TwoFactorResponse>();
        Assert.True(enabledPayload!.IsTwoFactorEnabled);

        return (token, sharedKey);
    }

    // An empty request returns the current state, shared key included.
    private async Task<string> FetchSharedKeyAsync(string token)
    {
        var keyResponse = await AuthedPostAsJsonAsync("/api/auth/manage/2fa", new { }, token);
        Assert.True(
            keyResponse.IsSuccessStatusCode,
            $"manage/2fa key fetch failed: {keyResponse.StatusCode} {await keyResponse.Content.ReadAsStringAsync()}"
        );
        var keyPayload = await keyResponse.Content.ReadFromJsonAsync<TwoFactorResponse>();
        Assert.NotNull(keyPayload);
        Assert.False(string.IsNullOrWhiteSpace(keyPayload!.SharedKey));
        return keyPayload.SharedKey;
    }

    private async Task RegisterAsync()
    {
        var registerResponse = await _client.PostAsJsonAsync(
            "/api/auth/register",
            new { email = _email, password = Password }
        );
        Assert.True(registerResponse.IsSuccessStatusCode);
    }

    private async Task<string> LoginAsync()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/auth/login",
            new { email = _email, password = Password }
        );
        Assert.True(response.IsSuccessStatusCode);
        var tokenResponse = await response.Content.ReadFromJsonAsync<AccessTokenResponse>();
        return tokenResponse!.AccessToken;
    }

    private async Task<JsonElement> AuthedGetJsonAsync(string uri, string token)
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await client.GetAsync(uri);
        Assert.True(
            response.IsSuccessStatusCode,
            $"GET {uri} failed: {response.StatusCode} {await response.Content.ReadAsStringAsync()}"
        );
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private Task<HttpResponseMessage> AuthedPostAsJsonAsync<T>(string uri, T payload, string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client.PostAsJsonAsync(uri, payload);
    }

    private static string GenerateTotp(string base32Secret)
    {
        var secret = DecodeBase32(base32Secret);
        var timestep = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30;
        Span<byte> data = stackalloc byte[8];
        for (var index = 7; index >= 0; index--)
        {
            data[index] = (byte)(timestep & 0xFF);
            timestep >>= 8;
        }

        using var hmac = new HMACSHA1(secret);
        var hash = hmac.ComputeHash(data.ToArray());
        var offset = hash[^1] & 0x0F;
        var binaryCode =
            ((hash[offset] & 0x7F) << 24)
            | ((hash[offset + 1] & 0xFF) << 16)
            | ((hash[offset + 2] & 0xFF) << 8)
            | (hash[offset + 3] & 0xFF);
        var otp = binaryCode % 1_000_000;
        return otp.ToString("D6");
    }

    /// <summary>
    /// Derived from the valid code so it cannot coincide with it, and so a rejection cannot be for
    /// the wrong reason — this is six digits Identity will parse and refuse.
    /// </summary>
    private static string WrongTotp(string base32Secret)
    {
        var valid = GenerateTotp(base32Secret);
        var firstDigit = (valid[0] - '0' + 1) % 10;
        return $"{firstDigit}{valid[1..]}";
    }

    private static byte[] DecodeBase32(string input)
    {
        var normalized = input.Trim().TrimEnd('=').Replace(" ", string.Empty).ToUpperInvariant();
        var buffer = new List<byte>(normalized.Length * 5 / 8);
        var bitsLeft = 0;
        var value = 0;

        foreach (var character in normalized)
        {
            var digit = character switch
            {
                >= 'A' and <= 'Z' => character - 'A',
                >= '2' and <= '7' => character - '2' + 26,
                _ => -1,
            };

            if (digit < 0)
            {
                continue;
            }

            value = (value << 5) | digit;
            bitsLeft += 5;

            if (bitsLeft >= 8)
            {
                buffer.Add((byte)(value >> (bitsLeft - 8)));
                bitsLeft -= 8;
            }
        }

        return buffer.ToArray();
    }

    private sealed record TwoFactorResponse(
        string SharedKey,
        int RecoveryCodesLeft,
        string[]? RecoveryCodes,
        bool IsTwoFactorEnabled,
        bool IsMachineRemembered
    );
}
