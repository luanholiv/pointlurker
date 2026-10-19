using FluentAssertions;
using PointLurker.Core.Domain.Identity;
using PointLurker.Core.Domain.Localization;

namespace Core.Domain.UnitTests.Identity;

public sealed class ApplicationUserTests
{
    [Fact]
    public void A_new_user_defaults_to_en_US()
    {
        new ApplicationUser().Locale.Should().Be(SupportedLocales.EnUs);
    }

    [Fact]
    public void SetLocale_stores_the_locale()
    {
        var user = new ApplicationUser();

        user.SetLocale(SupportedLocales.PtBr);

        user.Locale.Should().Be(SupportedLocales.PtBr);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null!)]
    public void SetLocale_rejects_an_empty_locale(string? locale)
    {
        var user = new ApplicationUser();

        var act = () => user.SetLocale(locale!);

        act.Should().Throw<ArgumentException>().WithParameterName("locale");
        user.Locale.Should().Be(SupportedLocales.EnUs);
    }
}
