using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using PointLurker.Core.Domain.Stores;

namespace PointLurker.Infrastructure.Database.Repositories;

/// <summary>
/// Every way one term is allowed to find an item. Two lists match items by text and they must agree
/// about what a term finds, so the arms live here rather than at each call site.
/// </summary>
/// <remarks>
/// Three name arms, because no one of them is enough on its own.
/// <list type="bullet">
///   <item>
///     Word matching, which is what makes a term of two words match a name holding them in the
///     other order, or split by a hyphen — a substring test reads "field tested" and "Field-Tested"
///     as different strings, and finds neither in the other.
///   </item>
///   <item>
///     Substring, which word matching alone cannot cover: the parser reads "Re.built" as one
///     lexeme, so "built" would not find it and "240" would not find "240Hz". It is also what makes
///     a half-typed word match at all, since a prefix is a substring.
///   </item>
///   <item>
///     Near-miss, for a misspelling — but only for a single word long enough to be worth guessing
///     at. A term of one or two characters resembles almost every name, and one of several words is
///     scored by the words it gets right rather than by the one it got wrong. Both conditions move
///     the threshold rather than the shape of the query; see
///     <see cref="StoreItemTextSearch.NearMissThreshold" />.
///   </item>
/// </list>
/// The text arms fold accents, matching what name ordering has always done: the same word arrives
/// accented in one row and bare in the next, so folding is the only way one query answers for both.
/// </remarks>
internal static class StoreItemTextMatch
{
    /// <summary>
    /// The name arms alone — what a list matches by when it renders the name and nothing else.
    /// </summary>
    internal static Expression<Func<StoreItem, bool>> ByName(string term)
    {
        var pattern = LikePattern.Containing(term);
        var nearMiss = StoreItemTextSearch.NearMissThreshold(term);

        return item =>
            EF.Functions.ToTsVector(StoreItemTextSearch.Configuration, item.Name)
                .Matches(EF.Functions.WebSearchToTsQuery(StoreItemTextSearch.Configuration, term))
            || EF.Functions.ILike(
                EF.Functions.Unaccent(item.Name),
                EF.Functions.Unaccent(pattern),
                LikePattern.EscapeCharacter
            )
            || EF.Functions.TrigramsWordSimilarity(term, EF.Functions.Unaccent(item.Name))
                >= nearMiss;
    }

    /// <summary>
    /// The name arms plus word matching over the description, for a list that renders the
    /// description too.
    /// </summary>
    /// <remarks>
    /// Word matching only over the description — one observed description is shared by 28 items, so
    /// a substring of it is evidence about the store's boilerplate rather than about the item.
    /// </remarks>
    internal static Expression<Func<StoreItem, bool>> ByNameOrDescription(string term)
    {
        var pattern = LikePattern.Containing(term);
        var nearMiss = StoreItemTextSearch.NearMissThreshold(term);

        return item =>
            EF.Functions.ToTsVector(StoreItemTextSearch.Configuration, item.Name)
                .Matches(EF.Functions.WebSearchToTsQuery(StoreItemTextSearch.Configuration, term))
            || EF.Functions.ILike(
                EF.Functions.Unaccent(item.Name),
                EF.Functions.Unaccent(pattern),
                LikePattern.EscapeCharacter
            )
            || EF.Functions.TrigramsWordSimilarity(term, EF.Functions.Unaccent(item.Name))
                >= nearMiss
            || EF.Functions.ToTsVector(
                    StoreItemTextSearch.Configuration,
                    item.Description ?? string.Empty
                )
                .Matches(EF.Functions.WebSearchToTsQuery(StoreItemTextSearch.Configuration, term));
    }
}
