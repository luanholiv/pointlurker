using Microsoft.Extensions.Options;

namespace PointLurker.Presentation.WebApi;

/// <summary>
/// Runs the application and reports configuration validation failures before the process dies.
/// </summary>
internal static class StartupRunner
{
    /// <summary>
    /// Runs <paramref name="initializeAsync"/> and then <paramref name="app"/>, logging every
    /// <see cref="OptionsValidationException"/> failure at <see cref="LogLevel.Critical"/> before
    /// rethrowing so the app still fails fast.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The logger is resolved before the run starts, and that order is load-bearing: a failed start
    /// disposes the host — and with it the service provider — in a <c>finally</c>, so resolving from
    /// the <c>catch</c> below throws <see cref="ObjectDisposedException"/> instead. That buries the
    /// failures we exist to report and makes the disposal error the last thing an operator sees.
    /// </para>
    /// <para>
    /// Pre-run initialization is inside the boundary because not every options failure waits for
    /// <c>ValidateOnStart</c>'s hosted service: EF builds the Identity model while migrations run,
    /// which resolves <c>IOptions&lt;IdentityOptions&gt;</c> and with it
    /// <see cref="Auth.IdentityPolicyOptions"/>. Left outside, an invalid lockout or password
    /// setting would kill the process with a bare stack trace instead of the Critical line.
    /// </para>
    /// </remarks>
    public static async Task RunReportingConfigurationFailuresAsync(
        WebApplication app,
        Func<Task> initializeAsync
    )
    {
        var logger = app.Services.GetRequiredService<ILogger<Program>>();

        try
        {
            await initializeAsync();
            await app.RunAsync();
        }
        catch (OptionsValidationException ex)
        {
            foreach (var failure in ex.Failures)
            {
                logger.LogConfigurationValidationFailure(failure);
            }

            throw;
        }
    }
}
