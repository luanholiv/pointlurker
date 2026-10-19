using System.Reflection;

namespace PointLurker.Architecture.Tests;

/// <summary>
/// Every searchable list endpoint answers with the same envelope. A shared generic
/// <c>PagedResponse&lt;T&gt;</c> cannot express that here — it would need one explicit conversion
/// operator per closed type, and its runtime name (<c>PagedResponse`1</c>) would slip past
/// <see cref="ExplicitOperatorTests.Every_response_contract_declares_an_operator_or_is_exempt" /> by
/// accident of backtick naming rather than by a recorded decision. Uniformity is enforced here
/// instead, the same way the repository enforces its other conventions: by reflection over the
/// shipped contracts.
/// </summary>
public class PageResponseShapeTests
{
    private const string PageSuffix = "PageResponse";

    private static readonly string[] Envelope = ["Items", "TotalCount", "Offset", "Limit"];

    [Fact]
    public void Every_page_response_declares_exactly_the_shared_envelope()
    {
        var offenders = PageResponses()
            .Select(contract => (contract, properties: PropertyNames(contract)))
            .Where(pair => !Matches(pair.properties, Envelope))
            .Select(pair => $"{pair.contract.FullName}: [{string.Join(", ", pair.properties)}]")
            .OrderBy(text => text, StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            $"""
            {nameof(
                Every_page_response_declares_exactly_the_shared_envelope
            )}: {offenders.Length} paged contract(s) do not declare exactly [{string.Join(
                ", ",
                Envelope
            )}].
            The envelope constrains the envelope, not the row — each endpoint stays free to choose
            what one row is, but a client must be able to read every list response the same way.
            Offenders:
            {string.Join("\n", offenders)}
            """
        );
    }

    /// <summary>
    /// Without this the shape rule above could be evaded by not using the name: a contract carrying
    /// items alongside a total <b>is</b> a page, whatever it is called.
    /// </summary>
    [Fact]
    public void A_contract_carrying_items_and_a_total_is_named_as_a_page_response()
    {
        var misnamed = Contracts()
            .Where(contract =>
            {
                var properties = PropertyNames(contract);
                return properties.Contains("Items")
                    && properties.Contains("TotalCount")
                    && !contract.Name.EndsWith(PageSuffix, StringComparison.Ordinal);
            })
            .Select(contract => contract.FullName ?? contract.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            misnamed.Length == 0,
            $"""
            {nameof(
                A_contract_carrying_items_and_a_total_is_named_as_a_page_response
            )}: {misnamed.Length} contract(s) are paged in all but name.
            Rename each to *{PageSuffix} so the shape rule applies to it.
            Misnamed:
            {string.Join("\n", misnamed)}
            """
        );
    }

    private static IEnumerable<Type> PageResponses() =>
        Contracts().Where(t => t.Name.EndsWith(PageSuffix, StringComparison.Ordinal));

    private static IEnumerable<Type> Contracts() =>
        ArchitectureAssemblies
            .Presentation.GetTypes()
            .Where(t =>
                t.Namespace?.EndsWith(".Contracts", StringComparison.Ordinal) == true
                && t.Name.EndsWith("Response", StringComparison.Ordinal)
            );

    // Records synthesize a protected EqualityContract, which the public-instance filter excludes.
    private static string[] PropertyNames(Type contract) =>
        [
            .. contract
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(p => p.Name),
        ];

    private static bool Matches(string[] actual, string[] expected) =>
        actual.Length == expected.Length && expected.All(actual.Contains);
}
