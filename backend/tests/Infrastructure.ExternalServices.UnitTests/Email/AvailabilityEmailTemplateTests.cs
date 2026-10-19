using FluentAssertions;
using PointLurker.Core.Application.Notifications;

namespace Infrastructure.ExternalServices.UnitTests.Email;

/// <summary>
/// What the availability email actually renders. Uses the real resource catalog, so these also
/// prove the shipped strings exist under the keys the template asks for.
/// </summary>
public sealed class AvailabilityEmailTemplateTests
{
    private const string BaseUrl = "https://monitor.example.com";

    private static string Render(AvailabilityEmailModel model, string? publicBaseUrl = null) =>
        AvailabilityEmailFixtures.Template(publicBaseUrl).RenderHtml(model);

    [Fact]
    public void Every_store_and_item_is_rendered()
    {
        var model = AvailabilityEmailFixtures.Model(
            AvailabilityEmailFixtures.Store(
                "Store A",
                AvailabilityEmailFixtures.Item("Item 1"),
                AvailabilityEmailFixtures.Item("Item 2")
            ),
            AvailabilityEmailFixtures.Store("Store B", AvailabilityEmailFixtures.Item("Item 3"))
        );

        var html = Render(model);

        html.Should()
            .Contain("Store A")
            .And.Contain("Store B")
            .And.Contain("Item 1")
            .And.Contain("Item 2")
            .And.Contain("Item 3");
    }

    [Fact]
    public void Summary_counts_items_and_stores()
    {
        var model = AvailabilityEmailFixtures.Model(
            AvailabilityEmailFixtures.Store(
                "Store A",
                AvailabilityEmailFixtures.Item("Item 1"),
                AvailabilityEmailFixtures.Item("Item 2")
            ),
            AvailabilityEmailFixtures.Store("Store B", AvailabilityEmailFixtures.Item("Item 3"))
        );

        var html = Render(model);

        html.Should().Contain("ITEMS").And.Contain("STORES");
    }

    [Fact]
    public void Summary_uses_singular_labels_for_a_single_item_in_a_single_store()
    {
        var html = Render(AvailabilityEmailFixtures.OneItemModel());

        html.Should().Contain(">ITEM<").And.Contain(">STORE<");
    }

    [Fact]
    public void Auto_redeem_tile_appears_only_when_something_is_pending()
    {
        var pending = AvailabilityEmailFixtures.Model(
            AvailabilityEmailFixtures.Store(
                "Store A",
                AvailabilityEmailFixtures.Item("Item 1", redemptionPending: true)
            )
        );

        Render(pending).Should().Contain("AUTO-REDEEMING");
        Render(AvailabilityEmailFixtures.OneItemModel()).Should().NotContain("AUTO-REDEEMING");
    }

    [Fact]
    public void Pending_item_carries_the_auto_redeem_pill()
    {
        var model = AvailabilityEmailFixtures.Model(
            AvailabilityEmailFixtures.Store(
                "Store A",
                AvailabilityEmailFixtures.Item("Cool Skin", redemptionPending: true)
            )
        );

        Render(model).Should().Contain("AUTO-REDEEM PENDING");
    }

    [Fact]
    public void Item_without_a_pending_redemption_carries_no_auto_redeem_pill()
    {
        Render(AvailabilityEmailFixtures.OneItemModel()).Should().NotContain("AUTO-REDEEM PENDING");
    }

    [Fact]
    public void Every_item_is_marked_available()
    {
        Render(AvailabilityEmailFixtures.OneItemModel()).Should().Contain("AVAILABLE");
    }

    [Fact]
    public void Cost_is_grouped_for_the_recipients_locale()
    {
        var model = AvailabilityEmailFixtures.Model(
            AvailabilityEmailFixtures.Store(
                "Store A",
                AvailabilityEmailFixtures.Item("Item", 12500L)
            )
        );

        AvailabilityEmailFixtures
            .InCulture("en-US", () => Render(model))
            .Should()
            .Contain("12,500");
        AvailabilityEmailFixtures
            .InCulture("pt-BR", () => Render(model))
            .Should()
            .Contain("12.500");
    }

    [Fact]
    public void Limited_stock_is_shown_and_unlimited_stock_is_not()
    {
        var limited = AvailabilityEmailFixtures.Model(
            AvailabilityEmailFixtures.Store(
                "Store A",
                AvailabilityEmailFixtures.Item("Item", quantityCurrent: 3, quantityTotal: 10)
            )
        );

        Render(limited).Should().Contain("STOCK: 3 / 10");
        Render(AvailabilityEmailFixtures.OneItemModel()).Should().NotContain("STOCK:");
    }

    [Fact]
    public void Subscriber_only_items_are_chipped()
    {
        var subOnly = AvailabilityEmailFixtures.Model(
            AvailabilityEmailFixtures.Store(
                "Store A",
                AvailabilityEmailFixtures.Item("Item", subscriberOnly: true)
            )
        );

        Render(subOnly).Should().Contain("SUB ONLY");
        Render(AvailabilityEmailFixtures.OneItemModel()).Should().NotContain("SUB ONLY");
    }

    [Fact]
    public void Category_is_chipped()
    {
        var model = AvailabilityEmailFixtures.Model(
            AvailabilityEmailFixtures.Store(
                "Store A",
                AvailabilityEmailFixtures.Item("Item", categoryName: "AUDIO")
            )
        );

        Render(model).Should().Contain("AUDIO");
    }

    [Fact]
    public void No_links_are_rendered_when_the_deployment_publishes_no_url()
    {
        var html = Render(AvailabilityEmailFixtures.OneItemModel());

        html.Should().NotContain("href=").And.NotContain("<a ");
    }

    [Fact]
    public void Links_point_at_the_wishlist_and_the_items_store_when_a_url_is_published()
    {
        var storeId = Guid.NewGuid();
        var model = AvailabilityEmailFixtures.Model(
            AvailabilityEmailFixtures.Store(
                "Store A",
                storeId,
                AvailabilityEmailFixtures.Item("Item")
            )
        );

        var html = Render(model, BaseUrl);

        html.Should()
            .Contain($"href=\"{BaseUrl}/wishlist\"")
            .And.Contain($"href=\"{BaseUrl}/stores/{storeId}\"");
    }

    [Fact]
    public void Preheader_states_the_count_and_is_hidden()
    {
        var html = Render(AvailabilityEmailFixtures.OneItemModel());

        html.Should().Contain("1 item is ready to redeem.").And.Contain("display:none");
    }

    [Fact]
    public void Color_scheme_is_pinned_so_clients_do_not_invert_the_palette()
    {
        var html = Render(AvailabilityEmailFixtures.OneItemModel());

        html.Should()
            .Contain("""<meta name="color-scheme" content="dark">""")
            .And.Contain("""<meta name="supported-color-schemes" content="dark">""");
    }

    [Fact]
    public void Document_language_follows_the_recipients_locale()
    {
        AvailabilityEmailFixtures
            .InCulture("pt-BR", () => Render(AvailabilityEmailFixtures.OneItemModel()))
            .Should()
            .Contain("""<html lang="pt-BR">""");
    }

    [Fact]
    public void Portuguese_renders_its_own_catalog()
    {
        var html = AvailabilityEmailFixtures.InCulture(
            "pt-BR",
            () => Render(AvailabilityEmailFixtures.OneItemModel())
        );

        html.Should().Contain("Itens ficaram disponíveis!").And.Contain("DISPONÍVEL");
    }

    [Fact]
    public void Markup_carries_nothing_a_mail_client_cannot_read()
    {
        var html = Render(AvailabilityEmailFixtures.OneItemModel(), BaseUrl);

        // The console's palette is authored in OKLCH behind custom properties; neither survives
        // in mail, so the template must ship resolved hex only.
        html.Should().NotContain("oklch(").And.NotContain("var(--");
    }

    [Fact]
    public void Store_and_item_names_are_escaped_exactly_once()
    {
        var model = AvailabilityEmailFixtures.Model(
            AvailabilityEmailFixtures.Store(
                """Store <A> & "B" """,
                AvailabilityEmailFixtures.Item("""Item <b>&"x"</b>""")
            )
        );

        var html = Render(model);

        html.Should().Contain("Store &lt;A&gt; &amp; &quot;B&quot;");
        html.Should().Contain("Item &lt;b&gt;&amp;&quot;x&quot;&lt;/b&gt;");
        html.Should().NotContain("&amp;lt;").And.NotContain("&amp;amp;");
    }

    [Fact]
    public void Template_never_emits_a_bold_tag()
    {
        // Not a style rule: the pipeline's escaping test proves markup in a name was neutralised
        // by asserting no bold tag survives, which only holds while the layout has none of its own.
        var html = Render(AvailabilityEmailFixtures.OneItemModel(), BaseUrl);

        html.Should().NotContain("<b>").And.NotContain("</b>");
    }

    [Fact]
    public void An_items_swatch_colour_depends_only_on_its_name_and_category()
    {
        var first = Render(
            AvailabilityEmailFixtures.Model(
                AvailabilityEmailFixtures.Store(
                    "Store A",
                    AvailabilityEmailFixtures.Item("Neon Skin Pack", categoryName: "SKIN")
                )
            )
        );
        var second = Render(
            AvailabilityEmailFixtures.Model(
                AvailabilityEmailFixtures.Store(
                    "Different Store",
                    AvailabilityEmailFixtures.Item(
                        "Neon Skin Pack",
                        cost: 999L,
                        categoryName: "SKIN"
                    )
                )
            )
        );

        // The console derives the swatch from name plus category alone, so the same item is the
        // same colour wherever it appears. Anything else in the model must not move it.
        Swatches(first).Should().ContainSingle().And.BeEquivalentTo(Swatches(second));
    }

    [Fact]
    public void Subject_is_singular_for_one_item_and_plural_beyond()
    {
        var template = AvailabilityEmailFixtures.Template();

        template
            .RenderSubject(AvailabilityEmailFixtures.OneItemModel())
            .Should()
            .Be("PointLurker: 1 item became available");

        template
            .RenderSubject(
                AvailabilityEmailFixtures.Model(
                    AvailabilityEmailFixtures.Store(
                        "Store A",
                        AvailabilityEmailFixtures.Item("Item 1"),
                        AvailabilityEmailFixtures.Item("Item 2")
                    )
                )
            )
            .Should()
            .Be("PointLurker: 2 items became available");
    }

    [Fact]
    public void Portuguese_subject_agrees_with_its_count()
    {
        var template = AvailabilityEmailFixtures.Template();

        AvailabilityEmailFixtures
            .InCulture(
                "pt-BR",
                () => template.RenderSubject(AvailabilityEmailFixtures.OneItemModel())
            )
            .Should()
            .Be("PointLurker: 1 item ficou disponível");

        AvailabilityEmailFixtures
            .InCulture(
                "pt-BR",
                () =>
                    template.RenderSubject(
                        AvailabilityEmailFixtures.Model(
                            AvailabilityEmailFixtures.Store(
                                "Store A",
                                AvailabilityEmailFixtures.Item("Item 1"),
                                AvailabilityEmailFixtures.Item("Item 2")
                            )
                        )
                    )
            )
            .Should()
            .Be("PointLurker: 2 itens ficaram disponíveis");
    }

    /// <summary>The distinct swatch fills in a rendered body, in document order.</summary>
    private static IReadOnlyList<string> Swatches(string html) =>
        System
            .Text.RegularExpressions.Regex.Matches(
                html,
                @"background-color:(#[0-9a-f]{6});border-radius:4px;""\s*>\s*<tr><td height=""64"""
            )
            .Select(m => m.Groups[1].Value)
            .Distinct()
            .ToArray();
}
