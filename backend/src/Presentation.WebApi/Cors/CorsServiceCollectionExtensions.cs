using PointLurker.Core.Application.Configuration;

namespace PointLurker.Presentation.WebApi.Cors;

public static class CorsServiceCollectionExtensions
{
    /// <summary>
    /// Registers the startup-validated <see cref="CorsSettings"/> and, unless CORS resolves to off,
    /// a default policy for the configured origins. No policy is registered at all when the origin
    /// list is empty outside Development, so no response can carry CORS headers.
    /// </summary>
    public static IServiceCollection AddConfiguredCors(
        this IServiceCollection services,
        IConfiguration configuration,
        bool isDevelopment
    )
    {
        services.AddValidatedOptions<CorsSettings, CorsSettingsValidator>(CorsSettings.SectionName);

        var (mode, origins) = CorsPolicyResolver.Resolve(configuration, isDevelopment);

        // No branch calls AllowCredentials: auth rides in the Authorization bearer header, never a
        // cookie, so no origin is ever granted ambient-credential access. (It is also illegal to
        // combine with AllowAnyOrigin.)
        if (mode is CorsMode.Restricted)
        {
            services.AddCors(options =>
                options.AddDefaultPolicy(policy =>
                    policy.WithOrigins(origins).AllowAnyMethod().AllowAnyHeader()
                )
            );
        }
        else if (mode is CorsMode.Permissive)
        {
            services.AddCors(options =>
                options.AddDefaultPolicy(policy =>
                    policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()
                )
            );
        }

        return services;
    }

    /// <summary>
    /// Adds the CORS middleware only when a policy was registered. In the shipped topology the
    /// frontend nginx proxies <c>/api</c>, so requests are same-origin and the middleware is absent.
    /// </summary>
    public static WebApplication UseConfiguredCors(this WebApplication app)
    {
        var (mode, _) = CorsPolicyResolver.Resolve(
            app.Configuration,
            app.Environment.IsDevelopment()
        );

        if (mode is not CorsMode.Disabled)
            app.UseCors();

        return app;
    }
}
