using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PointLurker.Core.Domain.Errors;
using PointLurker.Core.Domain.Repositories;
using PointLurker.Core.Domain.Stores;
using PointLurker.Presentation.WebApi.Search;

namespace Presentation.WebApi.UnitTests.Search;

/// <summary>
/// The shared query-string parser is where "reject, do not default" is enforced. A sort parameter
/// that silently fell back to the default would return correctly-shaped, wrongly-ordered data —
/// success as far as the caller can tell — so every unrecognized value has to surface as a 400
/// naming the parameter and its allowed set.
/// </summary>
/// <remarks>
/// Failures travel in the same <c>validation.failed</c> envelope a rejected request body uses: the
/// specific <c>search.*</c> code rides in a <c>params.errors</c> entry named by its query-string
/// parameter. That is what lets one frontend branch resolve both surfaces, and why these tests read
/// the nested entry rather than the outer code.
/// </remarks>
public sealed class SearchQueryTests
{
    /// <summary>
    /// The envelope is the contract; the code naming what actually broke is one level in.
    /// </summary>
    [Fact]
    public void A_rejection_answers_in_the_validation_failed_envelope()
    {
        var query = new SearchQuery();

        query.EnumOr(SearchParameters.Sort, "bogus", StoreItemSortField.Name);

        var problem = ProblemOf(query);
        problem.Status.Should().Be(StatusCodes.Status400BadRequest);
        problem.Extensions["code"].Should().Be(ErrorCodes.Validation.Failed);
    }

    [Fact]
    public void An_unknown_sort_field_is_rejected_with_code_and_params()
    {
        var query = new SearchQuery();

        query.EnumOr(SearchParameters.Sort, "bogus", StoreItemSortField.Name);

        var entry = FirstEntryOf(query);
        entry["code"].Should().Be(ErrorCodes.Search.InvalidEnumValue);
        entry["field"].Should().Be(SearchParameters.Sort);

        var entryParams = ParamsOf(entry);
        entryParams["parameter"].Should().Be(SearchParameters.Sort);
        entryParams["value"].Should().Be("bogus");
        // Read off the enum, so a new ordering reaches the caller's error without anyone
        // remembering to list it here — which is why "Relevance" is in this set.
        entryParams["allowed"]
            .Should()
            .BeEquivalentTo(new[] { "Name", "Cost", "Stock", "Date", "Relevance" });
    }

    /// <summary>
    /// The field is the query-string name, not a contract property name: it is what the caller
    /// wrote in the URL, so it is the only thing a message can usefully point at.
    /// </summary>
    [Fact]
    public void A_rejected_parameter_is_named_by_its_query_string_name()
    {
        var query = new SearchQuery();

        query.Identifier(SearchParameters.WishlistEntry, "abc");

        var entry = FirstEntryOf(query);
        entry["field"].Should().Be("wishlistEntryId");
        ParamsOf(entry)["field"].Should().Be("wishlistEntryId");
    }

    [Fact]
    public void An_unknown_sort_direction_is_rejected_with_its_allowed_values()
    {
        var query = new SearchQuery();

        query.EnumOr(SearchParameters.Direction, "sideways", SortDirection.Asc);

        var entry = FirstEntryOf(query);
        entry["code"].Should().Be(ErrorCodes.Search.InvalidEnumValue);
        ParamsOf(entry)["parameter"].Should().Be(SearchParameters.Direction);
        ParamsOf(entry)["allowed"].Should().BeEquivalentTo(new[] { "Asc", "Desc" });
    }

    [Fact]
    public void An_unknown_status_is_rejected_rather_than_treated_as_unfiltered()
    {
        var query = new SearchQuery();

        var status = query.OptionalEnum<RedemptionStatus>(SearchParameters.Status, "bogus");

        status.Should().BeNull();
        var entry = FirstEntryOf(query);
        entry["code"].Should().Be(ErrorCodes.Search.InvalidEnumValue);
        ParamsOf(entry)["parameter"].Should().Be(SearchParameters.Status);
    }

    [Fact]
    public void A_malformed_numeric_filter_is_rejected()
    {
        var query = new SearchQuery();

        query.Int64(SearchParameters.MinCost, "abc");

        ProblemOf(query).Status.Should().Be(StatusCodes.Status400BadRequest);
        var entry = FirstEntryOf(query);
        entry["code"].Should().Be(ErrorCodes.Search.InvalidFilterValue);
        ParamsOf(entry)["parameter"].Should().Be(SearchParameters.MinCost);
        ParamsOf(entry)["value"].Should().Be("abc");
    }

    /// <summary>
    /// Unlike the enum codes, a filter rejection has no allowed set to offer — the parameter takes
    /// any well-formed number, date or flag.
    /// </summary>
    [Fact]
    public void A_malformed_filter_offers_no_allowed_set()
    {
        var query = new SearchQuery();

        query.Int64(SearchParameters.MinCost, "abc");

        ParamsOf(FirstEntryOf(query)).Should().NotContainKey("allowed");
    }

    [Fact]
    public void A_malformed_timestamp_is_rejected()
    {
        var query = new SearchQuery();

        query.Timestamp(SearchParameters.From, "not-a-date");

        FirstEntryOf(query)["code"].Should().Be(ErrorCodes.Search.InvalidFilterValue);
    }

    [Fact]
    public void A_malformed_flag_is_rejected()
    {
        var query = new SearchQuery();

        query.Flag(SearchParameters.Available, "maybe");

        FirstEntryOf(query)["code"].Should().Be(ErrorCodes.Search.InvalidFilterValue);
    }

    [Fact]
    public void An_enum_ordinal_is_not_accepted_as_a_name()
    {
        // Enum.TryParse would happily read "1" as Cost. A query string names its values; a caller
        // sending an ordinal is relying on declaration order, which is not part of the contract.
        var query = new SearchQuery();

        query.EnumOr(SearchParameters.Sort, "1", StoreItemSortField.Name);

        FirstEntryOf(query)["code"].Should().Be(ErrorCodes.Search.InvalidEnumValue);
    }

    [Theory]
    [InlineData("name", StoreItemSortField.Name)]
    [InlineData("COST", StoreItemSortField.Cost)]
    [InlineData("Stock", StoreItemSortField.Stock)]
    [InlineData("date", StoreItemSortField.Date)]
    public void A_known_sort_field_parses_case_insensitively(
        string raw,
        StoreItemSortField expected
    )
    {
        var query = new SearchQuery();

        query.EnumOr(SearchParameters.Sort, raw, StoreItemSortField.Name).Should().Be(expected);

        query.Failure.Should().BeNull();
    }

    [Fact]
    public void An_omitted_enum_takes_the_documented_default()
    {
        var query = new SearchQuery();

        query
            .EnumOr(SearchParameters.Sort, null, StoreItemSortField.Name)
            .Should()
            .Be(StoreItemSortField.Name);
        query.OptionalEnum<RedemptionStatus>(SearchParameters.Status, null).Should().BeNull();
        query.Failure.Should().BeNull();
    }

    [Fact]
    public void An_omitted_filter_does_not_narrow_and_does_not_fail()
    {
        var query = new SearchQuery();

        query.Int64(SearchParameters.MinCost, null).Should().BeNull();
        query.Int32(SearchParameters.Limit, null).Should().BeNull();
        query.Flag(SearchParameters.SubscriberOnly, null).Should().BeNull();
        query.Timestamp(SearchParameters.From, null).Should().BeNull();
        query.Identifier(SearchParameters.Store, null).Should().BeNull();
        SearchQuery.Csv(null).Should().BeNull();

        query.Failure.Should().BeNull();
    }

    [Theory]
    [InlineData("1", true)]
    [InlineData("0", false)]
    [InlineData("true", true)]
    [InlineData("FALSE", false)]
    public void A_flag_accepts_both_the_numeric_and_the_boolean_spelling(string raw, bool expected)
    {
        var query = new SearchQuery();

        query.Flag(SearchParameters.Available, raw).Should().Be(expected);
        query.Failure.Should().BeNull();
    }

    [Fact]
    public void A_comma_separated_filter_splits_into_its_values()
    {
        SearchQuery.Csv("Emotes,Badges").Should().BeEquivalentTo(new[] { "Emotes", "Badges" });
    }

    [Fact]
    public void A_comma_separated_filter_drops_empty_segments()
    {
        SearchQuery.Csv("Emotes,,Badges,").Should().BeEquivalentTo(new[] { "Emotes", "Badges" });
    }

    [Fact]
    public void An_all_empty_comma_separated_filter_narrows_nothing()
    {
        SearchQuery.Csv(",,").Should().BeNull();
    }

    /// <summary>
    /// Every bad parameter is reported, in the order the handler read them. Naming only the first
    /// would make a caller with two typos fix them one round trip at a time.
    /// </summary>
    [Fact]
    public void Every_failure_is_reported_in_read_order()
    {
        var query = new SearchQuery();

        query.EnumOr(SearchParameters.Sort, "bogus", StoreItemSortField.Name);
        query.Int64(SearchParameters.MinCost, "abc");

        var entries = EntriesOf(query);
        entries.Should().HaveCount(2);

        entries[0]["field"].Should().Be(SearchParameters.Sort);
        entries[0]["code"].Should().Be(ErrorCodes.Search.InvalidEnumValue);
        entries[1]["field"].Should().Be(SearchParameters.MinCost);
        entries[1]["code"].Should().Be(ErrorCodes.Search.InvalidFilterValue);
    }

    [Fact]
    public void A_rejected_parameter_still_yields_its_fallback_so_parsing_can_continue()
    {
        var query = new SearchQuery();

        query
            .EnumOr(SearchParameters.Direction, "sideways", SortDirection.Desc)
            .Should()
            .Be(SortDirection.Desc);
    }

    [Theory]
    [InlineData(null, 48)]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(24, 24)]
    [InlineData(9999, 200)]
    public void A_slice_limit_is_clamped_to_its_documented_bounds(int? requested, int expected)
    {
        SearchQuery.ClampLimit(requested, fallback: 48, max: 200).Should().Be(expected);
    }

    [Theory]
    [InlineData(null, 0)]
    [InlineData(-1, 0)]
    [InlineData(0, 0)]
    [InlineData(120, 120)]
    public void A_slice_offset_is_never_negative(int? requested, int expected)
    {
        SearchQuery.ClampOffset(requested).Should().Be(expected);
    }

    [Fact]
    public void A_successful_parse_reports_no_failure()
    {
        var query = new SearchQuery();

        query.EnumOr(SearchParameters.Sort, "cost", StoreItemSortField.Name);
        query.EnumOr(SearchParameters.Direction, "desc", SortDirection.Asc);
        query.Int64(SearchParameters.MinCost, "100");
        query.Flag(SearchParameters.Available, "0");

        query.Failure.Should().BeNull();
    }

    private static ProblemDetails ProblemOf(SearchQuery query)
    {
        query.Failure.Should().NotBeNull("the parameter should have been rejected");
        return (ProblemDetails)
            query.Failure!.Should().BeAssignableTo<IValueHttpResult>().Subject.Value!;
    }

    private static IReadOnlyList<IReadOnlyDictionary<string, object?>> EntriesOf(SearchQuery query)
    {
        var wrapper = (IReadOnlyDictionary<string, object?>)ProblemOf(query).Extensions["params"]!;
        return ((IEnumerable<object?>)wrapper["errors"]!)
            .Cast<IReadOnlyDictionary<string, object?>>()
            .ToList();
    }

    private static IReadOnlyDictionary<string, object?> FirstEntryOf(SearchQuery query) =>
        EntriesOf(query)[0];

    private static IReadOnlyDictionary<string, object?> ParamsOf(
        IReadOnlyDictionary<string, object?> entry
    ) => (IReadOnlyDictionary<string, object?>)entry["params"]!;
}
