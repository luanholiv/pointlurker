using System.Reflection;
using FluentAssertions;
using PointLurker.Core.Domain.Errors;
using PointLurker.Core.Domain.Stores.Enums;

namespace Core.Domain.UnitTests.Errors;

/// <summary>
/// <see cref="RedemptionFailureCodes.For"/> ends in a catch-all arm, which it must: the failure code
/// is an integer read back out of the database, so an undeclared value is reachable and throwing
/// would fail an entire page of history over one row. The cost is that the compiler stops helping —
/// add a member to <see cref="SubmissionFailureCode"/> without a case and it compiles clean, then
/// renders as "unknown". These tests are what makes that a build failure instead.
/// </summary>
public sealed class RedemptionFailureCodesTests
{
    /// <summary>
    /// The <c>redemption.failure.*</c> constants, keyed by the C# member name they are declared
    /// under. Read by reflection so the pairing stays correct as codes are added, the way
    /// <c>Architecture.Tests/ErrorCodeCatalog</c> reads the taxonomy as a whole.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> DeclaredCodes =
        typeof(ErrorCodes.RedemptionFailure)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral)
            .ToDictionary(
                field => field.Name,
                field => (string)field.GetRawConstantValue()!,
                StringComparer.Ordinal
            );

    [Fact]
    public void Every_enum_member_maps_to_the_constant_of_the_same_name()
    {
        DeclaredCodes
            .Should()
            .NotBeEmpty("reflection that finds no constants would pass vacuously");

        foreach (var member in Enum.GetValues<SubmissionFailureCode>())
        {
            DeclaredCodes
                .Should()
                .ContainKey(
                    member.ToString(),
                    "every SubmissionFailureCode member needs an ErrorCodes.RedemptionFailure "
                        + "constant of the same name for the frontend catalog to resolve it"
                );

            RedemptionFailureCodes
                .For(member)
                .Should()
                .Be(
                    DeclaredCodes[member.ToString()],
                    "RedemptionFailureCodes.For fell through to the catch-all for {0}, so a real "
                        + "failure would reach the reader as \"unknown\"",
                    member
                );
        }
    }

    [Fact]
    public void Every_declared_code_is_produced_by_some_enum_member()
    {
        var produced = Enum.GetValues<SubmissionFailureCode>()
            .Select(RedemptionFailureCodes.For)
            .ToHashSet(StringComparer.Ordinal);

        produced
            .Should()
            .BeEquivalentTo(
                DeclaredCodes.Values,
                "a redemption.failure code no member produces is unreachable — delete the constant, "
                    + "or add the enum member it is waiting for"
            );
    }

    [Fact]
    public void An_undeclared_value_falls_back_to_unknown()
    {
        var undeclared = (SubmissionFailureCode)int.MaxValue;

        RedemptionFailureCodes
            .For(undeclared)
            .Should()
            .Be(
                ErrorCodes.RedemptionFailure.Unknown,
                "the column is an int EF casts back unvalidated, so a row written by a newer build "
                    + "must render as the catch-all rather than fail the page"
            );
    }
}
