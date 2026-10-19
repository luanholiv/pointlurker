using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PointLurker.Core.Domain.Errors;
using PointLurker.Presentation.WebApi.Errors;

namespace Presentation.WebApi.UnitTests.Errors;

public sealed class GlobalExceptionHandlerTests
{
    [Fact]
    public async Task HandleAsync_returns_500_with_internal_error_code()
    {
        var handler = new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance);
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        var ex = new InvalidOperationException("Something went wrong");

        var handled = await handler.TryHandleAsync(context, ex, CancellationToken.None);

        handled.Should().BeTrue();
        context.Response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        context.Response.ContentType.Should().StartWith("application/problem+json");

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = await JsonSerializer.DeserializeAsync<JsonElement>(context.Response.Body);
        body.GetProperty("code").GetString().Should().Be(ErrorCodes.System.InternalError);
        body.GetProperty("status").GetInt32().Should().Be(500);
    }

    [Fact]
    public async Task Bad_http_request_returns_its_own_status_with_malformed_body_code()
    {
        var context = NewContext("POST", "/api/users/me/locale");

        var body = await HandleAsync(
            context,
            new BadHttpRequestException("Failed to read parameter from the request body as JSON.")
        );

        context.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        context.Response.ContentType.Should().StartWith("application/problem+json");
        body.GetProperty("code").GetString().Should().Be(ErrorCodes.Request.MalformedBody);
        body.GetProperty("status").GetInt32().Should().Be(400);
        body.GetProperty("instance").GetString().Should().Be("/api/users/me/locale");
    }

    [Fact]
    public async Task Bad_http_request_body_carries_empty_params()
    {
        var context = NewContext("POST", "/api/auth/login");

        var body = await HandleAsync(context, new BadHttpRequestException("bad json"));

        // The malformed-body code has no interpolation params: the framework exception carries no
        // field-level detail, and its message must never reach the client.
        body.GetProperty("params").EnumerateObject().Should().BeEmpty();
        body.GetRawText().Should().NotContain("bad json");
    }

    [Fact]
    public async Task Oversized_request_body_maps_to_413_with_body_too_large_code()
    {
        var context = NewContext("PUT", "/api/users/me/se-token");

        var body = await HandleAsync(
            context,
            new BadHttpRequestException("Request body too large.", 413)
        );

        context.Response.StatusCode.Should().Be(StatusCodes.Status413PayloadTooLarge);
        body.GetProperty("code").GetString().Should().Be(ErrorCodes.Request.BodyTooLarge);
        body.GetProperty("status").GetInt32().Should().Be(413);
    }

    [Fact]
    public async Task Bad_http_request_with_server_status_stays_an_internal_error()
    {
        var context = NewContext("POST", "/api/stores");

        var body = await HandleAsync(context, new BadHttpRequestException("upstream blew up", 500));

        context.Response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        body.GetProperty("code").GetString().Should().Be(ErrorCodes.System.InternalError);
    }

    [Fact]
    public async Task Client_request_faults_are_logged_as_warnings()
    {
        var logger = new CapturingLogger<GlobalExceptionHandler>();
        var handler = new GlobalExceptionHandler(logger);
        var context = NewContext("POST", "/api/auth/login");

        await handler.TryHandleAsync(
            context,
            new BadHttpRequestException("bad json"),
            CancellationToken.None
        );

        logger.Entries.Should().ContainSingle().Which.Should().Be(LogLevel.Warning);
    }

    [Fact]
    public async Task Server_faults_are_logged_as_errors()
    {
        var logger = new CapturingLogger<GlobalExceptionHandler>();
        var handler = new GlobalExceptionHandler(logger);
        var context = NewContext("GET", "/api/stores");

        await handler.TryHandleAsync(
            context,
            new InvalidOperationException("boom"),
            CancellationToken.None
        );

        logger.Entries.Should().ContainSingle().Which.Should().Be(LogLevel.Error);
    }

    private static DefaultHttpContext NewContext(string method, string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static async Task<JsonElement> HandleAsync(HttpContext context, Exception exception)
    {
        var handler = new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance);

        var handled = await handler.TryHandleAsync(context, exception, CancellationToken.None);
        handled.Should().BeTrue();

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        return await JsonSerializer.DeserializeAsync<JsonElement>(context.Response.Body);
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<LogLevel> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter
        ) => Entries.Add(logLevel);
    }
}
