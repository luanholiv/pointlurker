using FluentAssertions;
using PointLurker.Core.Application.Stores;
using PointLurker.Core.Domain.Stores;
using PointLurker.Presentation.WebApi.Endpoints.Stores.Contracts;

namespace Presentation.WebApi.UnitTests.Endpoints.Stores.Contracts;

public sealed class StoreProfileResponseOperatorTests
{
    private static readonly DateTimeOffset Updated = new(2026, 8, 1, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Maps_full_field_set_and_converts_both_nested_families()
    {
        var profile = new StoreInputProfileResult(
            Unresolved: [new UnresolvedInputDto("Discord tag", "Discord", 82, null, 3, 1)],
            Resolved:
            [
                new ResolvedInputDto(
                    "Discord",
                    Updated,
                    [new InputRequesterDto("Discord tag", ResolutionSource.Fuzzy, 82, 3)]
                ),
            ],
            CanonicalLabels: ["Discord", "Nick"]
        );

        var response = (StoreProfileResponse)profile;

        response.Unresolved.Should().ContainSingle().Which.RawLabel.Should().Be("Discord tag");
        response.Resolved.Should().ContainSingle().Which.LabelCanonical.Should().Be("Discord");
        response.Resolved[0].RequestedBy.Should().ContainSingle().Which.Source.Should().Be("Fuzzy");
        response.CanonicalLabels.Should().Equal("Discord", "Nick");
    }

    [Fact]
    public void Maps_an_empty_profile()
    {
        var response = (StoreProfileResponse)new StoreInputProfileResult([], [], []);

        response.Unresolved.Should().BeEmpty();
        response.Resolved.Should().BeEmpty();
        response.CanonicalLabels.Should().BeEmpty();
    }
}

public sealed class StoreProfileUnresolvedInputResponseOperatorTests
{
    [Fact]
    public void Maps_full_field_set()
    {
        var dto = new UnresolvedInputDto(
            RawLabel: "Discord tag",
            BestCanonical: "Discord",
            BestScore: 82,
            AliasTarget: "Discord",
            ItemCount: 3,
            BlockedEntryCount: 1
        );

        var response = (StoreProfileUnresolvedInputResponse)dto;

        response
            .Should()
            .BeEquivalentTo(
                new StoreProfileUnresolvedInputResponse(
                    RawLabel: "Discord tag",
                    BestCanonical: "Discord",
                    BestScore: 82,
                    AliasTarget: "Discord",
                    ItemCount: 3,
                    BlockedEntryCount: 1
                )
            );
    }

    [Fact]
    public void Maps_a_label_with_no_candidate_or_alias()
    {
        var dto = new UnresolvedInputDto("Mystery", null, 0, null, 1, 0);

        var response = (StoreProfileUnresolvedInputResponse)dto;

        response.BestCanonical.Should().BeNull();
        response.AliasTarget.Should().BeNull();
        response.BestScore.Should().Be(0);
    }
}

public sealed class StoreProfileResolvedInputResponseOperatorTests
{
    [Fact]
    public void Maps_full_field_set_and_converts_requesters()
    {
        var updated = new DateTimeOffset(2026, 8, 1, 8, 0, 0, TimeSpan.Zero);
        var dto = new ResolvedInputDto(
            LabelCanonical: "Discord",
            UpdatedAt: updated,
            RequestedBy:
            [
                new InputRequesterDto("Discord tag", ResolutionSource.Alias, 100, 2),
                new InputRequesterDto("discord", ResolutionSource.Exact, 100, 1),
            ]
        );

        var response = (StoreProfileResolvedInputResponse)dto;

        response.LabelCanonical.Should().Be("Discord");
        response.UpdatedAt.Should().Be(updated);
        response.RequestedBy.Select(r => r.RawLabel).Should().Equal("Discord tag", "discord");
        response.RequestedBy.Select(r => r.Source).Should().Equal("Alias", "Exact");
    }

    [Fact]
    public void Maps_a_resolved_label_with_no_requesters()
    {
        var dto = new ResolvedInputDto("Discord", DateTimeOffset.UnixEpoch, []);

        var response = (StoreProfileResolvedInputResponse)dto;

        response.RequestedBy.Should().BeEmpty();
    }
}

public sealed class StoreProfileRequesterResponseOperatorTests
{
    [Theory]
    [InlineData(ResolutionSource.Alias, "Alias")]
    [InlineData(ResolutionSource.Fuzzy, "Fuzzy")]
    [InlineData(ResolutionSource.Unresolved, "Unresolved")]
    [InlineData(ResolutionSource.Exact, "Exact")]
    public void Serializes_the_resolution_source_as_its_name(
        ResolutionSource source,
        string expected
    )
    {
        var dto = new InputRequesterDto("Discord tag", source, 82, 3);

        var response = (StoreProfileRequesterResponse)dto;

        response.Source.Should().Be(expected);
    }

    [Fact]
    public void Maps_full_field_set()
    {
        var dto = new InputRequesterDto("Discord tag", ResolutionSource.Fuzzy, 82, 3);

        var response = (StoreProfileRequesterResponse)dto;

        response
            .Should()
            .BeEquivalentTo(
                new StoreProfileRequesterResponse(
                    RawLabel: "Discord tag",
                    Source: "Fuzzy",
                    Confidence: 82,
                    ItemCount: 3
                )
            );
    }
}

public sealed class StoreProfileValueUpsertRequestOperatorTests
{
    [Fact]
    public void Converts_the_request_into_the_application_command()
    {
        var request = new StoreProfileValueUpsertRequest("Discord", "user#1234");

        var upsert = (StoreProfileValueUpsert)request;

        upsert.Should().BeEquivalentTo(new StoreProfileValueUpsert("Discord", "user#1234"));
    }

    [Fact]
    public void Preserves_a_null_value_which_clears_the_stored_input()
    {
        var upsert = (StoreProfileValueUpsert)new StoreProfileValueUpsertRequest("Discord", null);

        upsert.Value.Should().BeNull();
    }
}
