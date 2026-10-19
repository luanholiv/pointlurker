using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace PointLurker.Core.Application.Configuration;

public static class ValidatedOptionsServiceCollectionExtensions
{
    /// <summary>
    /// Binds <typeparamref name="TOptions"/> to a configuration section and validates it at
    /// startup, so the app fails fast on a missing or invalid required value instead of throwing
    /// on first use.
    /// </summary>
    /// <remarks>
    /// <see cref="OptionsBuilderDataAnnotationsExtensions.ValidateDataAnnotations{TOptions}"/> is
    /// applied unconditionally: it is a no-op for options types that carry no annotations, which
    /// keeps every caller on one overload instead of a flag.
    /// </remarks>
    public static IServiceCollection AddValidatedOptions<TOptions>(
        this IServiceCollection services,
        string sectionName
    )
        where TOptions : class
    {
        services
            .AddOptions<TOptions>()
            .BindConfiguration(sectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        return services;
    }

    /// <inheritdoc cref="AddValidatedOptions{TOptions}(IServiceCollection, string)"/>
    /// <remarks>
    /// Adds <typeparamref name="TValidator"/> on top of the data-annotation validation described
    /// above. Registration order matters — the data-annotation validator is registered first so
    /// its failures are reported before <typeparamref name="TValidator"/>'s.
    /// </remarks>
    public static IServiceCollection AddValidatedOptions<TOptions, TValidator>(
        this IServiceCollection services,
        string sectionName
    )
        where TOptions : class
        where TValidator : class, IValidateOptions<TOptions>
    {
        services.AddValidatedOptions<TOptions>(sectionName);

        services.AddSingleton<IValidateOptions<TOptions>, TValidator>();

        return services;
    }
}
