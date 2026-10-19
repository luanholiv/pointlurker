using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using NetArchTest.Rules;
using PointLurker.Core.Application.Identity;
using PointLurker.Core.Application.Notifications;
using PointLurker.Core.Application.Stores;
using PointLurker.Infrastructure.Database.Identity;
using PointLurker.Infrastructure.ExternalServices.Email;
using PointLurker.Infrastructure.ExternalServices.GauPointsApi;
using PointLurker.Infrastructure.ExternalServices.StreamElementsApi;

namespace PointLurker.Architecture.Tests;

public class InfrastructureConventionTests
{
    private const string DatabaseProject = "Infrastructure.Database";

    /// <summary>The suffix that marks a repository read as returning change-tracked entities.</summary>
    private const string ForUpdateSuffix = "ForUpdateAsync";

    /// <summary>
    /// A method declaration in a hand-written file: an accessibility keyword, a return type, then the
    /// name and its parameter list. Type declarations are filtered out separately — a primary
    /// constructor puts a parameter list on the <c>class</c> line and would otherwise match.
    /// </summary>
    private static readonly Regex MethodDeclaration = new(
        @"(?m)^[ \t]*(?:public|internal|protected|private)[^\n(){};=]*?\s(?<name>\w+)\s*\(",
        RegexOptions.Compiled
    );

    [Fact]
    public void Refit_only_in_ExternalServices()
    {
        var nonExt = ArchitectureAssemblies
            .SourceAssemblies.Where(a => a != ArchitectureAssemblies.InfrastructureExternalServices)
            .ToArray();

        var result = Types.InAssemblies(nonExt).Should().NotHaveDependencyOn("Refit").GetResult();

        Assert.True(
            result.IsSuccessful,
            ArchAssert.Format(nameof(Refit_only_in_ExternalServices), result)
        );
    }

    [Fact]
    public void IStreamElementsApi_is_internal()
    {
        var type = typeof(IStreamElementsApi);
        Assert.False(
            type.IsVisible,
            $"{nameof(IStreamElementsApi_is_internal)}: {type.FullName} is visible (public). Expected internal."
        );
    }

    [Fact]
    public void IGauPointsApi_is_internal()
    {
        var type = typeof(IGauPointsApi);
        Assert.False(
            type.IsVisible,
            $"{nameof(IGauPointsApi_is_internal)}: {type.FullName} is visible (public). Expected internal."
        );
    }

    [Fact]
    public void Refit_DTOs_are_internal()
    {
        var asm = ArchitectureAssemblies.InfrastructureExternalServices;
        var dtoNames = new[]
        {
            "StoreItemResponse",
            "RedemptionResponse",
            "GetRedemptionResponse",
            "RedemptionItemRef",
            "RedeemerRef",
            "RedeemRequest",
            "ChannelLookupResponse",
            "GauPointsItemsResponse",
            "GauPointsItemResponse",
            "GauPointsMeResponse",
            "GauPointsUserResponse",
            "GauPointsRedeemRequest",
            "GauPointsRedeemData",
            "GauPointsRedeemResponse",
        };

        var leaked = new List<string>();
        foreach (var name in dtoNames)
        {
            var t = asm.GetTypes().FirstOrDefault(x => x.Name == name);
            if (t is null)
            {
                leaked.Add($"{name} (missing)");
                continue;
            }
            if (t.IsVisible)
            {
                leaked.Add(t.FullName ?? name);
            }
        }

        Assert.True(
            leaked.Count == 0,
            $"{nameof(Refit_DTOs_are_internal)}: {string.Join(", ", leaked)}"
        );
    }

    [Fact]
    public void EntityTypeConfiguration_only_in_Infrastructure_Database()
    {
        var nonDb = ArchitectureAssemblies
            .SourceAssemblies.Where(a => a != ArchitectureAssemblies.InfrastructureDatabase)
            .ToArray();

        var leaks = nonDb
            .SelectMany(a => a.GetTypes())
            .Where(t =>
                t.GetInterfaces()
                    .Any(i =>
                        i.IsGenericType
                        && i.GetGenericTypeDefinition() == typeof(IEntityTypeConfiguration<>)
                    )
            )
            .Select(t => t.FullName ?? t.Name)
            .ToArray();

        Assert.True(
            leaks.Length == 0,
            $"{nameof(EntityTypeConfiguration_only_in_Infrastructure_Database)}: {string.Join(", ", leaks)}"
        );
    }

    [Fact]
    public void EFCore_dependency_only_in_Infrastructure_Database_and_Presentation()
    {
        var forbidden = ArchitectureAssemblies
            .SourceAssemblies.Where(a =>
                a != ArchitectureAssemblies.InfrastructureDatabase
                && a != ArchitectureAssemblies.Presentation
            )
            .ToArray();

        var result = Types
            .InAssemblies(forbidden)
            .Should()
            .NotHaveDependencyOnAny("Microsoft.EntityFrameworkCore", "Npgsql")
            .GetResult();

        Assert.True(
            result.IsSuccessful,
            ArchAssert.Format(
                nameof(EFCore_dependency_only_in_Infrastructure_Database_and_Presentation),
                result
            )
        );
    }

    [Fact]
    public void StreamElementsApiClient_implements_IStoreProviderClient_and_is_sealed()
    {
        var t = typeof(StreamElementsApiClient);
        var implementsContract = typeof(IStoreProviderClient).IsAssignableFrom(t);
        Assert.True(
            t.IsSealed && implementsContract,
            $"{nameof(StreamElementsApiClient_implements_IStoreProviderClient_and_is_sealed)}: sealed={t.IsSealed}, implementsIStoreProviderClient={implementsContract}"
        );
    }

    [Fact]
    public void GauPointsApiClient_implements_IStoreProviderClient_and_is_sealed()
    {
        var t = typeof(GauPointsApiClient);
        var implementsContract = typeof(IStoreProviderClient).IsAssignableFrom(t);
        Assert.True(
            t.IsSealed && implementsContract,
            $"{nameof(GauPointsApiClient_implements_IStoreProviderClient_and_is_sealed)}: sealed={t.IsSealed}, implementsIStoreProviderClient={implementsContract}"
        );
    }

    [Fact]
    public void SingleUserProvider_implements_ISingleUserProvider_and_is_sealed()
    {
        var t = typeof(SingleUserProvider);
        var implementsContract = typeof(ISingleUserProvider).IsAssignableFrom(t);
        Assert.True(
            t.IsSealed && implementsContract,
            $"{nameof(SingleUserProvider_implements_ISingleUserProvider_and_is_sealed)}: sealed={t.IsSealed}, implementsISingleUserProvider={implementsContract}"
        );
    }

    [Fact]
    public void SmtpEmailService_implements_IEmailNotifier()
    {
        var t = typeof(SmtpEmailService);
        var implementsContract = typeof(IEmailNotifier).IsAssignableFrom(t);
        Assert.True(
            implementsContract,
            $"{nameof(SmtpEmailService_implements_IEmailNotifier)}: {t.FullName} does not implement {nameof(IEmailNotifier)}"
        );
    }

    [Fact]
    public void IEmailService_does_not_exist()
    {
        var asm = ArchitectureAssemblies.InfrastructureExternalServices;
        var found = asm.GetTypes().FirstOrDefault(t => t.Name == "IEmailService");
        Assert.True(found is null, $"{nameof(IEmailService_does_not_exist)}: {found?.FullName}");
    }

    /// <summary>
    /// <c>AppDbContext</c> sets <c>QueryTrackingBehavior.NoTracking</c> for every query it issues, so
    /// an <c>AsNoTracking()</c> call restates the default. Worse than redundant: it implies the
    /// default is tracking, which is what let the write paths drift into re-attaching detached graphs
    /// with <c>DbSet.Update</c>. Tracking is opted into, never out of.
    /// </summary>
    [Fact]
    public void AsNoTracking_is_not_used_in_Infrastructure_Database()
    {
        var violations = new List<string>();

        foreach (var file in SourceScan.CSharpFiles(SourcePaths.ProjectSourceRoot(DatabaseProject)))
        {
            foreach (var index in Occurrences(file.Text, "AsNoTracking("))
            {
                violations.Add(
                    $"{file.RelativePath}:{file.LineAt(index)}: {file.ExcerptAt(index)} "
                        + "— AppDbContext is already NoTracking; delete the call"
                );
            }
        }

        Assert.True(
            violations.Count == 0,
            $"""
            {nameof(
                AsNoTracking_is_not_used_in_Infrastructure_Database
            )}: Found {violations.Count} redundant AsNoTracking() call(s).
            Violations:
            {string.Join("\n", violations)}
            """
        );
    }

    /// <summary>
    /// The read/write split is carried by the method name: a read that hands back change-tracked
    /// entities — the only kind allowed to call <c>AsTracking()</c> — ends in
    /// <c>ForUpdateAsync</c>, and every <c>ForUpdateAsync</c> read tracks. Both directions matter. An
    /// untracked entity reaching a write method saves nothing, and a tracked entity handed to a
    /// read-only caller re-opens the accidental-write hole this convention closes, so a caller must
    /// be able to tell which it got from the name alone.
    /// </summary>
    [Fact]
    public void Tracked_reads_are_named_ForUpdateAsync()
    {
        var violations = new List<string>();

        foreach (var file in SourceScan.CSharpFiles(SourcePaths.ProjectSourceRoot(DatabaseProject)))
        {
            var methods = Methods(file).ToArray();

            foreach (var index in Occurrences(file.Text, "AsTracking("))
            {
                var owner = methods.LastOrDefault(method => method.Start < index);

                if (owner.Name is null)
                {
                    violations.Add(
                        $"{file.RelativePath}:{file.LineAt(index)}: AsTracking() outside any method — "
                            + "the guard cannot attribute it, so it cannot be sanctioned"
                    );
                }
                else if (!owner.Name.EndsWith(ForUpdateSuffix, StringComparison.Ordinal))
                {
                    violations.Add(
                        $"{file.RelativePath}:{file.LineAt(index)}: {owner.Name} calls AsTracking() but "
                            + $"is not named *{ForUpdateSuffix} — rename it so callers can see it returns tracked entities"
                    );
                }
            }

            foreach (var method in methods)
            {
                if (!method.Name.EndsWith(ForUpdateSuffix, StringComparison.Ordinal))
                {
                    continue;
                }

                if (!method.Body.Contains("AsTracking(", StringComparison.Ordinal))
                {
                    violations.Add(
                        $"{file.RelativePath}:{file.LineAt(method.Start)}: {method.Name} is named "
                            + $"*{ForUpdateSuffix} but never calls AsTracking() — it returns detached entities, "
                            + "so saving them would write nothing"
                    );
                }
            }
        }

        Assert.True(
            violations.Count == 0,
            $"""
            {nameof(
                Tracked_reads_are_named_ForUpdateAsync
            )}: Found {violations.Count} tracking-convention violation(s).
            Violations:
            {string.Join("\n", violations)}
            """
        );
    }

    /// <summary>
    /// Committing is the unit of work's job and nothing else's. A repository that calls
    /// <c>SaveChangesAsync</c> is a transaction boundary of its own, which is what made a use case
    /// writing two aggregates produce two commits — and what made the redemption channel publishable
    /// between them. The guard is here rather than in review because the tempting shape is one line.
    /// </summary>
    [Fact]
    public void SaveChangesAsync_is_called_only_by_the_unit_of_work()
    {
        const string UnitOfWorkFile = "UnitOfWork.cs";

        var violations = new List<string>();

        foreach (var file in SourceScan.CSharpFiles(SourcePaths.ProjectSourceRoot(DatabaseProject)))
        {
            if (file.RelativePath == UnitOfWorkFile)
            {
                continue;
            }

            foreach (var index in Occurrences(file.Text, "SaveChangesAsync("))
            {
                violations.Add(
                    $"{file.RelativePath}:{file.LineAt(index)}: {file.ExcerptAt(index)} "
                        + $"— stage the change and let IUnitOfWork commit it; only {UnitOfWorkFile} may save"
                );
            }
        }

        Assert.True(
            violations.Count == 0,
            $"""
            {nameof(
                SaveChangesAsync_is_called_only_by_the_unit_of_work
            )}: Found {violations.Count} call(s) outside {UnitOfWorkFile}.
            Violations:
            {string.Join("\n", violations)}
            """
        );
    }

    /// <summary>
    /// Method declarations in <paramref name="file"/>, each paired with the text running to the next
    /// declaration (its body, near enough for a spelling check). Interface members are included:
    /// their bodyless declarations carry no <c>AsTracking()</c>, which is why the
    /// <c>ForUpdateAsync</c> direction of the guard only scans implementation projects.
    /// </summary>
    private static IEnumerable<(string Name, int Start, string Body)> Methods(ScannedFile file)
    {
        var declarations = MethodDeclaration
            .Matches(file.Text)
            .Where(match => !IsTypeDeclaration(match.Value))
            .ToArray();

        for (var i = 0; i < declarations.Length; i++)
        {
            var start = declarations[i].Index;
            var end = i + 1 < declarations.Length ? declarations[i + 1].Index : file.Text.Length;

            yield return (declarations[i].Groups["name"].Value, start, file.Text[start..end]);
        }
    }

    /// <summary>
    /// True when a <see cref="MethodDeclaration"/> match is really a type declaration. A primary
    /// constructor (<c>sealed class FooRepository(AppDbContext dbContext)</c>) has the same shape as a
    /// method, and would otherwise be reported as the owner of everything below it.
    /// </summary>
    private static bool IsTypeDeclaration(string declaration) =>
        declaration.Contains(" class ", StringComparison.Ordinal)
        || declaration.Contains(" record ", StringComparison.Ordinal)
        || declaration.Contains(" struct ", StringComparison.Ordinal)
        || declaration.Contains(" interface ", StringComparison.Ordinal);

    private static IEnumerable<int> Occurrences(string text, string value)
    {
        for (
            var index = text.IndexOf(value, StringComparison.Ordinal);
            index >= 0;
            index = text.IndexOf(value, index + 1, StringComparison.Ordinal)
        )
        {
            yield return index;
        }
    }
}
