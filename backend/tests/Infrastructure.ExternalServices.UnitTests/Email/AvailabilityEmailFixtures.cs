using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using PointLurker.Core.Application.Notifications;
using PointLurker.Infrastructure.ExternalServices.Email;

namespace Infrastructure.ExternalServices.UnitTests.Email;

/// <summary>
/// Shared builders for the email tests. The localizer is the real one over the shipped
/// <c>.resx</c> files rather than a substitute, so an assertion that a rendered string is present
/// also proves the catalog carries the key.
/// </summary>
internal static class AvailabilityEmailFixtures
{
    public static IStringLocalizer<EmailResources> RealLocalizer()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLocalization(options => options.ResourcesPath = "Resources");
        return services
            .BuildServiceProvider()
            .GetRequiredService<IStringLocalizer<EmailResources>>();
    }

    public static AvailabilityEmailTemplate Template(string? publicBaseUrl = null) =>
        new(RealLocalizer(), Options.Create(new AppUrlSettings { PublicBaseUrl = publicBaseUrl }));

    public static FulfillmentCodeEmailTemplate FulfillmentTemplate(string? publicBaseUrl = null) =>
        new(RealLocalizer(), Options.Create(new AppUrlSettings { PublicBaseUrl = publicBaseUrl }));

    public static CredentialExpiryEmailTemplate ExpiryTemplate(string? publicBaseUrl = null) =>
        new(RealLocalizer(), Options.Create(new AppUrlSettings { PublicBaseUrl = publicBaseUrl }));

    public static AvailabilityEmailItem Item(
        string name,
        long cost = 100L,
        bool redemptionPending = false,
        string categoryName = "SKIN",
        bool subscriberOnly = false,
        int quantityCurrent = -1,
        int quantityTotal = -1
    ) =>
        new(
            name,
            cost,
            redemptionPending,
            categoryName,
            subscriberOnly,
            quantityCurrent,
            quantityTotal
        );

    public static AvailabilityEmailStoreGroup Store(
        string name,
        params AvailabilityEmailItem[] items
    ) => new(name, Guid.NewGuid(), items);

    public static AvailabilityEmailStoreGroup Store(
        string name,
        Guid watchedStoreId,
        params AvailabilityEmailItem[] items
    ) => new(name, watchedStoreId, items);

    public static AvailabilityEmailModel Model(params AvailabilityEmailStoreGroup[] stores) =>
        new(stores);

    /// <summary>One store, one plain item — the shape most tests need when the model is not
    /// what they are exercising.</summary>
    public static AvailabilityEmailModel OneItemModel(string itemName = "Cool Item") =>
        Model(Store("Store A", Item(itemName)));

    /// <summary>
    /// Runs <paramref name="render"/> under a named culture and restores the ambient one.
    /// </summary>
    /// <remarks>
    /// The template reads the ambient culture, which the send path sets per recipient. A test that
    /// renders directly inherits whatever the runner happens to be under — invariant on CI — so any
    /// comparison against a rendered message has to pin the culture on both sides.
    /// </remarks>
    public static T InCulture<T>(string locale, Func<T> render)
    {
        var previousUi = CultureInfo.CurrentUICulture;
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentUICulture = new CultureInfo(locale);
        CultureInfo.CurrentCulture = new CultureInfo(locale);

        try
        {
            return render();
        }
        finally
        {
            CultureInfo.CurrentUICulture = previousUi;
            CultureInfo.CurrentCulture = previous;
        }
    }
}
