using System.Text.Json;
using FluentAssertions;
using PointLurker.Infrastructure.ExternalServices.GauPointsApi;
using Refit;
using Xunit.Abstractions;

namespace PointLurker.Infrastructure.ExternalServices.ContractTests.GauPointsApi;

/// <summary>
/// Provokes the one refusal nobody has seen: a redeem the balance cannot cover. It sends a real
/// POST to the live site with a live session, so it never runs in CI — its category is excluded by
/// every filter the repository runs, and it is skipped unless <c>GAUPOINTS_PROBE_COOKIE</c> is set.
/// Run it by hand, then record what it prints in <c>docs/GAUPOINTS-API.md</c> §3.1 and pin it with a
/// contract test.
/// </summary>
/// <remarks>
/// The guard is computed, never written down: cost and balance both move, and a number copied into
/// a test goes stale in the direction that spends points. Every precondition is asserted before
/// the POST, so a failed one sends nothing.
/// </remarks>
[Trait("Category", "LiveProbeWrite")]
public sealed class GauPointsRedeemLiveProbeTests(ITestOutputHelper output)
{
    private static readonly string BaseUrl =
        Environment.GetEnvironmentVariable("GAUPOINTS_PROBE_BASE_URL")
        ?? "https://gaupoints.com.br";

    private readonly IGauPointsApi _api = RestService.For<IGauPointsApi>(BaseUrl);

    /// <summary>
    /// Syntactically valid and belonging to nobody, so a server that validates the form before the
    /// balance still gets as far as the balance. <c>example.com</c> is reserved for this.
    /// </summary>
    private static readonly GauPointsRedeemData Recipient = new()
    {
        FullName = "Live Probe",
        Phone = "+5511900000000",
        Email = "probe@example.com",
        Address = "Rua Exemplo, 0, Sao Paulo - SP, 00000-000",
    };

    [GauPointsProbeCookieFact]
    public async Task An_unaffordable_redeem_is_refused_and_spends_nothing()
    {
        var cookie =
            $"kp_session={Environment.GetEnvironmentVariable(GauPointsProbeCookieFactAttribute.VariableName)}";

        // The catalogue first and the balance last, so the balance the guard trusts is the freshest
        // reading taken before the POST.
        var catalogue = await _api.GetItemsAsync();
        var before = await _api.GetMeAsync(cookie);

        before.Logged.Should().BeTrue("the session must be live, or the refusal is the 401 again");
        before.Points.Should().NotBeNull("a signed-in session reports its balance");
        var balance = before.Points!.Value;

        // In stock, so the request reaches the points check whatever order the server validates in.
        // Unaffordable, so it cannot succeed. The costliest, for the widest margin over the balance.
        var target = catalogue
            .Items.Where(item => item.Stock > 0 && item.Cost > balance)
            .MaxBy(item => item.Cost);

        target
            .Should()
            .NotBeNull(
                "an in-stock item must cost more than the balance of {0}, or nothing can be probed safely",
                balance
            );
        target!.Cost.Should().BeGreaterThan(balance);

        output.WriteLine(
            $"Probing '{target.Id}' ({target.Type}): cost {target.Cost}, balance {balance}."
        );

        var response = await _api.RedeemAsync(
            new GauPointsRedeemRequest { ItemId = target.Id, Data = Recipient },
            cookie,
            Guid.NewGuid().ToString()
        );

        var envelope = ReadEnvelope(response);
        var after = await _api.GetMeAsync(cookie);

        // The code of an accepted redeem is a bearer secret, so it is reported as present, never
        // printed. Everything else is what this probe exists to capture.
        output.WriteLine($"Status: {(int?)response.StatusCode} {response.StatusCode}");
        output.WriteLine($"ok: {envelope?.Ok?.ToString() ?? "(absent)"}");
        output.WriteLine($"error: {envelope?.Error ?? "(absent)"}");
        output.WriteLine($"message: {envelope?.Message ?? "(absent)"}");
        output.WriteLine($"code: {(envelope?.Code is null ? "(absent)" : "(present, withheld)")}");
        output.WriteLine($"Balance after: {after.Points?.ToString() ?? "(absent)"}");
        if (envelope is null)
            output.WriteLine($"Unparsed body: {ErrorBody(response) ?? "(empty)"}");

        after.Points.Should().Be(balance, "an unaffordable redeem must not move the balance");
        envelope.Should().NotBeNull("a refusal answers with the JSON envelope");
        envelope!.Ok.Should().BeFalse("a redeem the balance cannot cover is a refusal");
        envelope.Code.Should().BeNull("a refusal hands out no gift card");
    }

    private static GauPointsRedeemResponse? ReadEnvelope(
        IApiResponse<GauPointsRedeemResponse> response
    )
    {
        if (response.IsSuccessStatusCode)
            return response.Content;

        var body = ErrorBody(response);
        if (string.IsNullOrWhiteSpace(body))
            return null;

        try
        {
            return JsonSerializer.Deserialize<GauPointsRedeemResponse>(body);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? ErrorBody(IApiResponse<GauPointsRedeemResponse> response) =>
        response.HasResponseError(out var error) ? error.Content : null;
}
