using PointLurker.Core.Application;
using PointLurker.Core.Domain.Identity;
using PointLurker.Infrastructure.Database;
using PointLurker.Infrastructure.ExternalServices.Email;
using PointLurker.Infrastructure.ExternalServices.GauPointsApi;
using PointLurker.Infrastructure.ExternalServices.StreamElementsApi;
using PointLurker.Presentation.WebApi;
using PointLurker.Presentation.WebApi.Auth;
using PointLurker.Presentation.WebApi.Cors;
using PointLurker.Presentation.WebApi.Endpoints.Auth;
using PointLurker.Presentation.WebApi.Endpoints.Redemptions;
using PointLurker.Presentation.WebApi.Endpoints.Stores;
using PointLurker.Presentation.WebApi.Endpoints.Users;
using PointLurker.Presentation.WebApi.Endpoints.Wishlist;
using PointLurker.Presentation.WebApi.Errors;
using PointLurker.Presentation.WebApi.HostedServices;
using PointLurker.Presentation.WebApi.Validation;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();
builder.Services.AddApiErrorHandling();
builder.Services.AddRequestValidation();

var connectionString =
    builder.Configuration.GetConnectionString("Database")
    ?? throw new InvalidOperationException("Connection string 'Database' not found.");

builder.Services.AddInfrastructureDatabase(connectionString);

builder.Services.AddTokenEncryption();

builder.Services.AddStreamElementsApi();
builder.Services.AddGauPointsApi();

builder.Services.AddSmtpEmail();

builder.Services.AddCoreApplication();

builder.Services.AddBackgroundJobs();

builder.Services.AddAppAuthentication();

// CORS is off unless `Cors:AllowedOrigins` is set — in the shipped topology the frontend nginx
// proxies /api, so requests are same-origin. Development falls back to a permissive policy for the
// cross-origin Vite dev server.
builder.Services.AddConfiguredCors(builder.Configuration, builder.Environment.IsDevelopment());

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseExceptionHandler();
app.UseConfiguredCors();
app.UseAuthentication();
app.UseAuthorization();

// Suppress unexposed MapIdentityApi endpoints before endpoint binding runs.
app.UseSuppressedIdentityEndpoints();

// MapIdentityApi writes its own failure shapes, which carry no error code. This buffers those
// responses and adds the {code, params} contract every other endpoint already returns.
app.UseIdentityErrorContract();

// Framework Identity API: register, login, refresh, manage/2fa, manage/info. The middleware
// (registered above, before endpoint binding) suppresses the endpoints we do not expose
// (reset/confirm + GET manage/info).
app.MapGroup("/api/auth").MapIdentityApi<ApplicationUser>();

app.MapAuthEndpoints();
app.MapUserEndpoints();
app.MapStoreEndpoints();
app.MapStoreLookupEndpoints();
app.MapWishlistEndpoints();
app.MapRedemptionEndpoints();
app.MapHealthChecks("/health");
app.MapGet("/", () => Results.Ok("PointLurker API"));

await StartupRunner.RunReportingConfigurationFailuresAsync(
    app,
    async () =>
    {
        using var scope = app.Services.CreateScope();
        var initializer = scope.ServiceProvider.GetRequiredService<IDatabaseInitializer>();
        await initializer.InitializeAsync();
    }
);

public partial class Program;
