using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PointLurker.Presentation.WebApi.Cors;

namespace Presentation.WebApi.UnitTests.Cors;

// Proves the registration wires the validator, so a malformed origin fails fast rather than
// silently never matching. Program.cs pairs this with ValidateOnStart, which moves the same failure
// to startup.
public sealed class CorsServiceCollectionExtensionsTests
{
    [Fact]
    public void A_malformed_origin_fails_options_validation()
    {
        var provider = BuildProvider("https://app.example.com/");

        var act = () => provider.GetRequiredService<IOptions<CorsSettings>>().Value;

        act.Should().Throw<OptionsValidationException>().WithMessage("*AllowedOrigins[0]*");
    }

    [Fact]
    public void A_valid_origin_passes_options_validation()
    {
        var provider = BuildProvider("https://app.example.com");

        var options = provider.GetRequiredService<IOptions<CorsSettings>>().Value;

        options.AllowedOrigins.Should().Equal("https://app.example.com");
    }

    [Fact]
    public void No_configured_origins_binds_to_an_empty_list()
    {
        var provider = BuildProvider();

        var options = provider.GetRequiredService<IOptions<CorsSettings>>().Value;

        options.AllowedOrigins.Should().BeEmpty();
    }

    private static ServiceProvider BuildProvider(params string[] origins)
    {
        var values = new Dictionary<string, string?>();
        for (var i = 0; i < origins.Length; i++)
        {
            values[$"{CorsSettings.SectionName}:AllowedOrigins:{i}"] = origins[i];
        }

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddConfiguredCors(configuration, isDevelopment: false);

        return services.BuildServiceProvider();
    }
}
