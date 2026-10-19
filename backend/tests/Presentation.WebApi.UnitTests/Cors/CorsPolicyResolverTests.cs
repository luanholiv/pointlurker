using FluentAssertions;
using Microsoft.Extensions.Configuration;
using PointLurker.Presentation.WebApi.Cors;

namespace Presentation.WebApi.UnitTests.Cors;

public sealed class CorsPolicyResolverTests
{
    private static IConfiguration Configuration(params string[] origins)
    {
        var values = new Dictionary<string, string?>();
        for (var i = 0; i < origins.Length; i++)
        {
            values[$"{CorsSettings.SectionName}:AllowedOrigins:{i}"] = origins[i];
        }

        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    [Fact]
    public void Configured_origins_restrict_outside_development()
    {
        var (mode, origins) = CorsPolicyResolver.Resolve(
            Configuration("https://app.example.com"),
            isDevelopment: false
        );

        mode.Should().Be(CorsMode.Restricted);
        origins.Should().Equal("https://app.example.com");
    }

    [Fact]
    public void Configured_origins_win_in_development()
    {
        var (mode, origins) = CorsPolicyResolver.Resolve(
            Configuration("https://app.example.com"),
            isDevelopment: true
        );

        mode.Should().Be(CorsMode.Restricted);
        origins.Should().Equal("https://app.example.com");
    }

    [Fact]
    public void Empty_origins_fall_back_to_permissive_in_development()
    {
        var (mode, origins) = CorsPolicyResolver.Resolve(Configuration(), isDevelopment: true);

        mode.Should().Be(CorsMode.Permissive);
        origins.Should().BeEmpty();
    }

    [Fact]
    public void Empty_origins_disable_cors_outside_development()
    {
        var (mode, origins) = CorsPolicyResolver.Resolve(Configuration(), isDevelopment: false);

        mode.Should().Be(CorsMode.Disabled);
        origins.Should().BeEmpty();
    }

    [Fact]
    public void Missing_section_disables_cors_outside_development()
    {
        var configuration = new ConfigurationBuilder().Build();

        var (mode, origins) = CorsPolicyResolver.Resolve(configuration, isDevelopment: false);

        mode.Should().Be(CorsMode.Disabled);
        origins.Should().BeEmpty();
    }

    [Fact]
    public void Blank_entries_are_dropped()
    {
        var (mode, origins) = CorsPolicyResolver.Resolve(
            Configuration("", "   "),
            isDevelopment: false
        );

        mode.Should().Be(CorsMode.Disabled);
        origins.Should().BeEmpty();
    }

    [Fact]
    public void Origins_are_trimmed_and_lowercased()
    {
        var (mode, origins) = CorsPolicyResolver.Resolve(
            Configuration("  HTTPS://App.Example.COM  "),
            isDevelopment: false
        );

        mode.Should().Be(CorsMode.Restricted);
        origins.Should().Equal("https://app.example.com");
    }

    [Fact]
    public void Duplicate_origins_are_deduped()
    {
        var (mode, origins) = CorsPolicyResolver.Resolve(
            Configuration(
                "https://app.example.com",
                "HTTPS://APP.EXAMPLE.COM",
                "https://b.example.com"
            ),
            isDevelopment: false
        );

        mode.Should().Be(CorsMode.Restricted);
        origins.Should().Equal("https://app.example.com", "https://b.example.com");
    }
}
