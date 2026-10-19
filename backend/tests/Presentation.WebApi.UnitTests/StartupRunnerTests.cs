using System.Collections.Concurrent;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PointLurker.Presentation.WebApi;

namespace Presentation.WebApi.UnitTests;

/// <summary>
/// Guards the resolve-then-run ordering: the logger must be resolved before the host runs,
/// because a failed start disposes the service provider. If the resolve moves back inside the
/// <c>catch</c>, <see cref="StartupRunnerTests.RunReportingConfigurationFailuresAsync_logs_every_validation_failure_then_rethrows"/>
/// fails with <see cref="ObjectDisposedException"/> instead of the expected
/// <see cref="OptionsValidationException"/>.
/// </summary>
public sealed class StartupRunnerTests
{
    private const int ConfigurationValidationFailureEventId = 4001;
    private const string FirstFailure = "ProbeSettings: first probe failure";
    private const string SecondFailure = "ProbeSettings: second probe failure";
    private const string BootFailure = "probe hosted service refused to start";

    [Fact]
    public async Task RunReportingConfigurationFailuresAsync_logs_every_validation_failure_then_rethrows()
    {
        var recorder = new RecordingLoggerProvider();
        var app = CreateApp(
            recorder,
            services =>
                services
                    .AddOptions<ProbeSettings>()
                    .Validate(_ => false, FirstFailure)
                    .Validate(_ => false, SecondFailure)
                    .ValidateOnStart()
        );

        var run = () =>
            StartupRunner.RunReportingConfigurationFailuresAsync(app, () => Task.CompletedTask);

        var thrown = await run.Should().ThrowAsync<OptionsValidationException>();
        var failures = thrown.Which.Failures.ToList();
        failures.Should().Contain([FirstFailure, SecondFailure]);

        // Filtered because the host logs its own "Hosting failed to start" error into the same
        // recorder; what matters is one Critical report per failure, none missing, none duplicated.
        var reported = recorder
            .Records.Where(record => record.EventId.Id == ConfigurationValidationFailureEventId)
            .ToList();

        reported.Should().HaveCount(failures.Count);
        reported.Should().AllSatisfy(record => record.Level.Should().Be(LogLevel.Critical));

        foreach (var failure in failures)
        {
            reported
                .Should()
                .ContainSingle(record =>
                    record.Message.Contains(failure, StringComparison.Ordinal)
                );
        }
    }

    /// <summary>
    /// Not every options failure waits for <c>ValidateOnStart</c>'s hosted service: EF builds the
    /// Identity model while migrations run, which resolves <c>IOptions&lt;IdentityOptions&gt;</c> and
    /// so validates <c>IdentityPolicyOptions</c> before the host ever starts. That step has to be
    /// inside the reporting boundary or the operator gets a bare stack trace.
    /// </summary>
    [Fact]
    public async Task RunReportingConfigurationFailuresAsync_reports_failures_from_the_initialization_step()
    {
        var recorder = new RecordingLoggerProvider();
        var app = CreateApp(recorder, _ => { });

        var run = () =>
            StartupRunner.RunReportingConfigurationFailuresAsync(
                app,
                () =>
                    throw new OptionsValidationException(
                        nameof(ProbeSettings),
                        typeof(ProbeSettings),
                        [FirstFailure]
                    )
            );

        await run.Should().ThrowAsync<OptionsValidationException>();

        recorder
            .Records.Should()
            .ContainSingle(record =>
                record.EventId.Id == ConfigurationValidationFailureEventId
                && record.Level == LogLevel.Critical
                && record.Message.Contains(FirstFailure, StringComparison.Ordinal)
            );
    }

    [Fact]
    public async Task RunReportingConfigurationFailuresAsync_does_not_report_unrelated_startup_failures()
    {
        var recorder = new RecordingLoggerProvider();
        var app = CreateApp(
            recorder,
            services => services.AddHostedService<ThrowingHostedService>()
        );

        var run = () =>
            StartupRunner.RunReportingConfigurationFailuresAsync(app, () => Task.CompletedTask);

        (await run.Should().ThrowAsync<InvalidOperationException>()).WithMessage(BootFailure);

        recorder
            .Records.Should()
            .NotContain(record => record.EventId.Id == ConfigurationValidationFailureEventId);
    }

    private static WebApplication CreateApp(
        RecordingLoggerProvider recorder,
        Action<IServiceCollection> configureServices
    )
    {
        var builder = WebApplication.CreateSlimBuilder();

        // Port 0 keeps Kestrel off a fixed port: it starts before any hosted service we register, so
        // the second test would otherwise contend for the default dev port.
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        builder.Logging.AddProvider(recorder);
        configureServices(builder.Services);

        return builder.Build();
    }

    private sealed class ProbeSettings;

    private sealed class ThrowingHostedService : IHostedService
    {
        public Task StartAsync(CancellationToken cancellationToken) =>
            throw new InvalidOperationException(BootFailure);

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed record LogRecord(LogLevel Level, EventId EventId, string Message);

    private sealed class RecordingLoggerProvider : ILoggerProvider
    {
        private readonly ConcurrentQueue<LogRecord> records = new();

        public IReadOnlyCollection<LogRecord> Records => records;

        public ILogger CreateLogger(string categoryName) => new RecordingLogger(records);

        public void Dispose() { }

        private sealed class RecordingLogger(ConcurrentQueue<LogRecord> records) : ILogger
        {
            public IDisposable BeginScope<TState>(TState state)
                where TState : notnull => NoOpScope.Instance;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter
            ) => records.Enqueue(new LogRecord(logLevel, eventId, formatter(state, exception)));
        }

        private sealed class NoOpScope : IDisposable
        {
            public static readonly NoOpScope Instance = new();

            public void Dispose() { }
        }
    }
}
