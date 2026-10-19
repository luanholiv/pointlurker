using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PointLurker.Core.Application.Configuration;
using PointLurker.Core.Application.Stores;

namespace Core.Application.UnitTests.Configuration;

/// <summary>
/// Exercised through <see cref="SyncSettings"/> because it is the one options type that carries
/// both data annotations and a custom <see cref="IValidateOptions{TOptions}"/> — the combination
/// the helper has to get right. The same type covers the validator-less overload: a value only
/// <see cref="SyncSettingsValidator"/> rejects proves that overload leaves it unregistered.
/// </summary>
public class ValidatedOptionsServiceCollectionExtensionsTests
{
    [Fact]
    public void Binds_the_named_configuration_section_onto_the_options_instance()
    {
        var provider = BuildProvider(
            new Dictionary<string, string?>
            {
                ["Sync:FetchCronExpression"] = "*/5 * * * *",
                ["Sync:UsernameRefreshCronExpression"] = "0 3 * * *",
                ["Sync:MaxDegreeOfParallelism"] = "7",
                ["Sync:ResolutionLogRetentionDays"] = "14",
            }
        );

        var options = provider.GetRequiredService<IOptions<SyncSettings>>().Value;

        options.FetchCronExpression.Should().Be("*/5 * * * *");
        options.UsernameRefreshCronExpression.Should().Be("0 3 * * *");
        options.MaxDegreeOfParallelism.Should().Be(7);
        options.ResolutionLogRetentionDays.Should().Be(14);
    }

    [Fact]
    public void Registers_the_validator_as_a_singleton_IValidateOptions()
    {
        var services = new ServiceCollection();

        services.AddValidatedOptions<SyncSettings, SyncSettingsValidator>(SyncSettings.SectionName);

        var descriptor = services.Single(d =>
            d.ServiceType == typeof(IValidateOptions<SyncSettings>)
            && d.ImplementationType == typeof(SyncSettingsValidator)
        );

        descriptor.Lifetime.Should().Be(ServiceLifetime.Singleton);
    }

    [Fact]
    public void Registers_the_data_annotations_validator_before_the_custom_validator()
    {
        var services = new ServiceCollection();

        services.AddValidatedOptions<SyncSettings, SyncSettingsValidator>(SyncSettings.SectionName);

        var validators = services
            .Where(d => d.ServiceType == typeof(IValidateOptions<SyncSettings>))
            .ToList();

        // ValidateDataAnnotations() registers its validator as a pre-built instance, so it is the
        // one with no implementation type. Order matters: it must run before the custom validator
        // so failure messages keep surfacing in the order the app has always reported them.
        var annotationsIndex = validators.FindIndex(d => d.ImplementationType is null);
        var customIndex = validators.FindIndex(d =>
            d.ImplementationType == typeof(SyncSettingsValidator)
        );

        annotationsIndex.Should().BeGreaterThanOrEqualTo(0);
        customIndex.Should().BeGreaterThan(annotationsIndex);
    }

    [Fact]
    public void Fails_validation_when_the_custom_validator_rejects_the_bound_values()
    {
        var provider = BuildProvider(
            new Dictionary<string, string?>
            {
                ["Sync:FetchCronExpression"] = "not-a-cron",
                ["Sync:MaxDegreeOfParallelism"] = "4",
            }
        );

        var act = () => provider.GetRequiredService<IOptions<SyncSettings>>().Value;

        act.Should()
            .Throw<OptionsValidationException>()
            .WithMessage("*FetchCronExpression*not-a-cron*");
    }

    [Fact]
    public void Fails_validation_when_a_data_annotation_is_violated()
    {
        var provider = BuildProvider(
            new Dictionary<string, string?> { ["Sync:MaxDegreeOfParallelism"] = "999" }
        );

        var act = () => provider.GetRequiredService<IOptions<SyncSettings>>().Value;

        act.Should().Throw<OptionsValidationException>().WithMessage("*MaxDegreeOfParallelism*");
    }

    [Fact]
    public void Enables_startup_validation()
    {
        var services = new ServiceCollection();

        services.AddValidatedOptions<SyncSettings, SyncSettingsValidator>(SyncSettings.SectionName);

        // ValidateOnStart() is observable through the IStartupValidator it registers. Matched by
        // name so the test does not pin the concrete framework implementation type.
        services.Should().Contain(d => d.ServiceType.Name == "IStartupValidator");
    }

    [Fact]
    public void Binds_the_named_configuration_section_without_a_custom_validator()
    {
        var provider = BuildProviderWithoutValidator(
            new Dictionary<string, string?>
            {
                ["Sync:FetchCronExpression"] = "*/5 * * * *",
                ["Sync:MaxDegreeOfParallelism"] = "7",
            }
        );

        var options = provider.GetRequiredService<IOptions<SyncSettings>>().Value;

        options.FetchCronExpression.Should().Be("*/5 * * * *");
        options.MaxDegreeOfParallelism.Should().Be(7);
    }

    [Fact]
    public void Registers_only_the_data_annotations_validator_without_a_custom_validator()
    {
        var services = new ServiceCollection();

        services.AddValidatedOptions<SyncSettings>(SyncSettings.SectionName);

        var validators = services
            .Where(d => d.ServiceType == typeof(IValidateOptions<SyncSettings>))
            .ToList();

        // The data-annotations validator is registered as a pre-built instance, so it is the one
        // with no implementation type — and it must be the only one this overload contributes.
        validators.Should().ContainSingle().Which.ImplementationType.Should().BeNull();
    }

    [Fact]
    public void Fails_validation_when_a_data_annotation_is_violated_without_a_custom_validator()
    {
        var provider = BuildProviderWithoutValidator(
            new Dictionary<string, string?> { ["Sync:MaxDegreeOfParallelism"] = "999" }
        );

        var act = () => provider.GetRequiredService<IOptions<SyncSettings>>().Value;

        act.Should().Throw<OptionsValidationException>().WithMessage("*MaxDegreeOfParallelism*");
    }

    [Fact]
    public void Accepts_a_value_that_only_the_custom_validator_would_have_rejected()
    {
        // Passes [Required] but not SyncSettingsValidator's cron parse, so it only binds cleanly
        // when the validator-less overload really does leave the custom validator unregistered.
        var provider = BuildProviderWithoutValidator(
            new Dictionary<string, string?> { ["Sync:FetchCronExpression"] = "not-a-cron" }
        );

        var options = provider.GetRequiredService<IOptions<SyncSettings>>().Value;

        options.FetchCronExpression.Should().Be("not-a-cron");
    }

    [Fact]
    public void Enables_startup_validation_without_a_custom_validator()
    {
        var services = new ServiceCollection();

        services.AddValidatedOptions<SyncSettings>(SyncSettings.SectionName);

        services.Should().Contain(d => d.ServiceType.Name == "IStartupValidator");
    }

    private static ServiceProvider BuildProvider(Dictionary<string, string?> configuration)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(
            new ConfigurationBuilder().AddInMemoryCollection(configuration).Build()
        );
        services.AddValidatedOptions<SyncSettings, SyncSettingsValidator>(SyncSettings.SectionName);
        return services.BuildServiceProvider();
    }

    private static ServiceProvider BuildProviderWithoutValidator(
        Dictionary<string, string?> configuration
    )
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(
            new ConfigurationBuilder().AddInMemoryCollection(configuration).Build()
        );
        services.AddValidatedOptions<SyncSettings>(SyncSettings.SectionName);
        return services.BuildServiceProvider();
    }
}
