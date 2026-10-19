using System.Reflection;

namespace PointLurker.Architecture.Tests;

public class ExplicitOperatorTests
{
    [Fact]
    public void StoreItemResponse_defines_explicit_operator_to_ProviderStoreItem()
    {
        AssertExplicitOperatorExists(
            sourceTypeFullName: "PointLurker.Infrastructure.ExternalServices.StreamElementsApi.StoreItemResponse",
            sourceAssembly: ArchitectureAssemblies.InfrastructureExternalServices,
            targetTypeFullName: "PointLurker.Core.Application.Stores.ProviderStoreItem",
            targetAssembly: ArchitectureAssemblies.Application,
            ruleName: nameof(StoreItemResponse_defines_explicit_operator_to_ProviderStoreItem)
        );
    }

    [Fact]
    public void GauPointsItemResponse_defines_explicit_operator_to_ProviderStoreItem()
    {
        AssertExplicitOperatorExists(
            sourceTypeFullName: "PointLurker.Infrastructure.ExternalServices.GauPointsApi.GauPointsItemResponse",
            sourceAssembly: ArchitectureAssemblies.InfrastructureExternalServices,
            targetTypeFullName: "PointLurker.Core.Application.Stores.ProviderStoreItem",
            targetAssembly: ArchitectureAssemblies.Application,
            ruleName: nameof(GauPointsItemResponse_defines_explicit_operator_to_ProviderStoreItem)
        );
    }

    [Fact]
    public void GauPointsRedeemRequest_defines_explicit_operator_from_ProviderRedeemCommand()
    {
        var commandType = ArchitectureAssemblies.Application.GetType(
            "PointLurker.Core.Application.Stores.ProviderRedeemCommand"
        );
        var requestType = ArchitectureAssemblies.InfrastructureExternalServices.GetType(
            "PointLurker.Infrastructure.ExternalServices.GauPointsApi.GauPointsRedeemRequest"
        );

        Assert.True(
            commandType is not null && requestType is not null,
            $"{nameof(GauPointsRedeemRequest_defines_explicit_operator_from_ProviderRedeemCommand)}: command or request type missing"
        );

        var op = requestType!.GetMethod(
            "op_Explicit",
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types: [commandType!],
            modifiers: null
        );

        Assert.True(
            op is not null && op.ReturnType == requestType,
            $"{nameof(GauPointsRedeemRequest_defines_explicit_operator_from_ProviderRedeemCommand)}: expected public static {requestType.FullName} op_Explicit({commandType!.FullName})"
        );
    }

    [Fact]
    public void RedemptionResponse_defines_explicit_operator_to_ProviderRedemptionResult()
    {
        AssertExplicitOperatorExists(
            sourceTypeFullName: "PointLurker.Infrastructure.ExternalServices.StreamElementsApi.RedemptionResponse",
            sourceAssembly: ArchitectureAssemblies.InfrastructureExternalServices,
            targetTypeFullName: "PointLurker.Core.Application.Stores.ProviderRedemptionResult",
            targetAssembly: ArchitectureAssemblies.Application,
            ruleName: nameof(
                RedemptionResponse_defines_explicit_operator_to_ProviderRedemptionResult
            )
        );
    }

    /// <summary>
    /// The single-redemption lookup maps through its own contract, not the redeem response's. The
    /// two payloads disagree on <c>item</c>, so one shared type deserializes for only one of them.
    /// </summary>
    [Fact]
    public void GetRedemptionResponse_defines_explicit_operator_to_ProviderRedemptionStatus()
    {
        AssertExplicitOperatorExists(
            sourceTypeFullName: "PointLurker.Infrastructure.ExternalServices.StreamElementsApi.GetRedemptionResponse",
            sourceAssembly: ArchitectureAssemblies.InfrastructureExternalServices,
            targetTypeFullName: "PointLurker.Core.Application.Stores.ProviderRedemptionStatus",
            targetAssembly: ArchitectureAssemblies.Application,
            ruleName: nameof(
                GetRedemptionResponse_defines_explicit_operator_to_ProviderRedemptionStatus
            )
        );
    }

    [Fact]
    public void ChannelLookupResponse_defines_explicit_operator_to_ProviderStoreLookup()
    {
        AssertExplicitOperatorExists(
            sourceTypeFullName: "PointLurker.Infrastructure.ExternalServices.StreamElementsApi.ChannelLookupResponse",
            sourceAssembly: ArchitectureAssemblies.InfrastructureExternalServices,
            targetTypeFullName: "PointLurker.Core.Application.Stores.ProviderStoreLookup",
            targetAssembly: ArchitectureAssemblies.Application,
            ruleName: nameof(ChannelLookupResponse_defines_explicit_operator_to_ProviderStoreLookup)
        );
    }

    [Fact]
    public void RedeemRequest_defines_explicit_operator_from_ProviderRedeemCommand()
    {
        // Inverse direction: op_Explicit(ProviderRedeemCommand) returning RedeemRequest, declared on RedeemRequest.
        var commandTypeFullName = "PointLurker.Core.Application.Stores.ProviderRedeemCommand";
        var requestTypeFullName =
            "PointLurker.Infrastructure.ExternalServices.StreamElementsApi.RedeemRequest";

        var commandType = ArchitectureAssemblies.Application.GetType(commandTypeFullName);
        var requestType = ArchitectureAssemblies.InfrastructureExternalServices.GetType(
            requestTypeFullName
        );

        Assert.True(
            commandType is not null,
            $"{nameof(RedeemRequest_defines_explicit_operator_from_ProviderRedeemCommand)}: missing {commandTypeFullName}"
        );
        Assert.True(
            requestType is not null,
            $"{nameof(RedeemRequest_defines_explicit_operator_from_ProviderRedeemCommand)}: missing {requestTypeFullName}"
        );

        var op = requestType!.GetMethod(
            "op_Explicit",
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types: [commandType!],
            modifiers: null
        );

        Assert.True(
            op is not null && op.ReturnType == requestType,
            $"{nameof(RedeemRequest_defines_explicit_operator_from_ProviderRedeemCommand)}: expected public static {requestType.FullName} op_Explicit({commandType!.FullName}) on {requestType.FullName}"
        );
    }

    private const string Contracts = "PointLurker.Presentation.WebApi.Endpoints.";
    private const string App = "PointLurker.Core.Application.Stores.";
    private const string DomainStores = "PointLurker.Core.Domain.Stores.";
    private const string DomainRepositories = "PointLurker.Core.Domain.Repositories.";
    private const string DomainIdentity = "PointLurker.Core.Domain.Identity.";
    private const string AppIdentity = "PointLurker.Core.Application.Identity.";

    /// <summary>
    /// Presentation converts in the opposite direction to Infrastructure: the operator is declared
    /// on the contract and takes the application or domain record it projects. StoreDetailResponse
    /// appears twice because it converts from both a bare store and the add-store result that
    /// carries the missing labels.
    /// </summary>
    public static TheoryData<string, string> PresentationResponseOperators { get; } =
        new()
        {
            {
                Contracts + "Stores.Contracts.StoreListItemResponse",
                DomainRepositories + "WatchedStoreListRow"
            },
            { Contracts + "Stores.Contracts.StorePageResponse", App + "WatchedStoreListResult" },
            {
                Contracts + "Stores.Contracts.StoreSummaryResponse",
                DomainRepositories + "WatchedStoreTotals"
            },
            {
                Contracts + "Stores.Contracts.StoreDetailResponse",
                DomainRepositories + "WatchedStoreDetailRow"
            },
            { Contracts + "Stores.Contracts.StoreDetailResponse", App + "AddWatchedStoreResult" },
            { Contracts + "Stores.Contracts.StoreItemDetailResponse", DomainStores + "StoreItem" },
            {
                Contracts + "Stores.Contracts.StoreItemRowResponse",
                DomainRepositories + "StoreItemListRow"
            },
            { Contracts + "Stores.Contracts.StoreItemPageResponse", App + "StoreItemListResult" },
            {
                Contracts + "Stores.Contracts.StoreItemFacetsResponse",
                DomainRepositories + "StoreItemFacets"
            },
            {
                Contracts + "Stores.Contracts.StoreProfileResponse",
                App + "StoreInputProfileResult"
            },
            {
                Contracts + "Stores.Contracts.StoreProfileUnresolvedInputResponse",
                App + "UnresolvedInputDto"
            },
            {
                Contracts + "Stores.Contracts.StoreProfileResolvedInputResponse",
                App + "ResolvedInputDto"
            },
            {
                Contracts + "Stores.Contracts.StoreProfileRequesterResponse",
                App + "InputRequesterDto"
            },
            {
                Contracts + "Wishlist.Contracts.CreateWishlistEntryResponse",
                App + "AddWishlistEntrySuccess"
            },
            {
                Contracts + "Wishlist.Contracts.WishlistPageResponse",
                App + "WishlistGroupedResponse"
            },
            {
                Contracts + "Wishlist.Contracts.WishlistStoreGroupResponse",
                App + "WishlistStoreGroup"
            },
            {
                Contracts + "Wishlist.Contracts.WishlistEntryListItemResponse",
                App + "WishlistEntryGroupItem"
            },
            {
                Contracts + "Wishlist.Contracts.WishlistEntryDetailResponse",
                App + "GetWishlistEntryResult"
            },
            {
                Contracts + "Redemptions.Contracts.RedemptionPageResponse",
                App + "RedemptionListResult"
            },
            { Contracts + "Redemptions.Contracts.RedemptionItemResponse", App + "RedemptionDto" },
            { Contracts + "Users.Contracts.CurrentUserResponse", AppIdentity + "CurrentUser" },
            {
                Contracts + "Users.Contracts.CredentialSummaryResponse",
                DomainIdentity + "ProviderCredential"
            },
            { Contracts + "Stores.Contracts.StoreLookupResponse", App + "ProviderStoreLookup" },
        };

    /// <summary>
    /// Request contracts convert the other way — the operator is still declared on the contract,
    /// but it takes the contract and returns the application command, exactly like RedeemRequest.
    /// </summary>
    public static TheoryData<string, string> PresentationRequestOperators { get; } =
        new()
        {
            {
                Contracts + "Stores.Contracts.StoreProfileValueUpsertRequest",
                App + "StoreProfileValueUpsert"
            },
        };

    [Theory]
    [MemberData(nameof(PresentationResponseOperators))]
    public void Response_contract_declares_explicit_operator_from_its_source(
        string contractTypeFullName,
        string sourceTypeFullName
    )
    {
        const string Rule = nameof(Response_contract_declares_explicit_operator_from_its_source);

        var contractType = ArchitectureAssemblies.Presentation.GetType(contractTypeFullName);
        var sourceType = ResolveApplicationOrDomainType(sourceTypeFullName);

        Assert.True(contractType is not null, $"{Rule}: missing {contractTypeFullName}");
        Assert.True(sourceType is not null, $"{Rule}: missing {sourceTypeFullName}");

        var op = contractType!.GetMethod(
            "op_Explicit",
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types: [sourceType!],
            modifiers: null
        );

        Assert.True(
            op is not null && op.ReturnType == contractType,
            $"{Rule}: expected public static {contractType.FullName} op_Explicit({sourceType!.FullName}) on {contractType.FullName}"
        );
    }

    [Theory]
    [MemberData(nameof(PresentationRequestOperators))]
    public void Request_contract_declares_explicit_operator_to_its_target(
        string contractTypeFullName,
        string targetTypeFullName
    )
    {
        AssertExplicitOperatorExists(
            sourceTypeFullName: contractTypeFullName,
            sourceAssembly: ArchitectureAssemblies.Presentation,
            targetTypeFullName: targetTypeFullName,
            targetAssembly: ArchitectureAssemblies.Application,
            ruleName: nameof(Request_contract_declares_explicit_operator_to_its_target)
        );
    }

    /// <summary>
    /// The table above only proves the operators it lists exist. This is what makes the convention
    /// mandatory: a new response contract that nobody adds to the table fails the build.
    /// </summary>
    [Fact]
    public void Every_response_contract_declares_an_operator_or_is_exempt()
    {
        // Projects a bare bool — there is no application result record behind it.
        string[] exempt = [Contracts + "Auth.Contracts.BootstrapResponse"];

        var covered = PresentationResponseOperators
            .Select(row => (string)row[0]!)
            .Concat(exempt)
            .ToHashSet(StringComparer.Ordinal);

        var uncovered = ArchitectureAssemblies
            .Presentation.GetTypes()
            .Where(t =>
                t.Namespace?.EndsWith(".Contracts", StringComparison.Ordinal) == true
                && t.Name.EndsWith("Response", StringComparison.Ordinal)
            )
            .Select(t => t.FullName ?? t.Name)
            .Where(name => !covered.Contains(name))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            uncovered.Length == 0,
            $"""
            {nameof(
                Every_response_contract_declares_an_operator_or_is_exempt
            )}: {uncovered.Length} response contract(s) have no explicit operator.
            Declare `public static explicit operator <Contract>(<SourceRecord>)` on each and add it
            to PresentationResponseOperators, or add it to the exempt list with a reason.
            Uncovered:
            {string.Join("\n", uncovered)}
            """
        );
    }

    [Fact]
    public void No_mapping_library_referenced()
    {
        var srcDir = SourcePaths.BackendSourceRoot;
        var csprojs = Directory.GetFiles(srcDir, "*.csproj", SearchOption.AllDirectories);

        var leaks = new List<string>();
        var forbidden = new[] { "AutoMapper", "Mapster" };

        foreach (var csproj in csprojs)
        {
            var content = File.ReadAllText(csproj);
            foreach (var pkg in forbidden)
            {
                if (
                    content.Contains($"\"{pkg}\"", StringComparison.OrdinalIgnoreCase)
                    || content.Contains($"\"{pkg}.", StringComparison.OrdinalIgnoreCase)
                )
                {
                    leaks.Add($"{Path.GetFileName(csproj)}: {pkg}");
                }
            }
        }

        // Also inspect Directory.Packages.props — required, not best-effort: central package
        // management is where a mapping library would be declared, so skipping it when the file is
        // not found would be skipping the half of the check that matters.
        var packagesProps = File.ReadAllText(SourcePaths.BackendFile("Directory.Packages.props"));
        foreach (var pkg in forbidden)
        {
            if (
                packagesProps.Contains($"\"{pkg}\"", StringComparison.OrdinalIgnoreCase)
                || packagesProps.Contains($"\"{pkg}.", StringComparison.OrdinalIgnoreCase)
            )
            {
                leaks.Add($"Directory.Packages.props: {pkg}");
            }
        }

        Assert.True(
            leaks.Count == 0,
            $"{nameof(No_mapping_library_referenced)}: {string.Join(", ", leaks)}"
        );
    }

    private static void AssertExplicitOperatorExists(
        string sourceTypeFullName,
        Assembly sourceAssembly,
        string targetTypeFullName,
        Assembly targetAssembly,
        string ruleName
    )
    {
        var sourceType = sourceAssembly.GetType(sourceTypeFullName);
        var targetType = targetAssembly.GetType(targetTypeFullName);

        Assert.True(sourceType is not null, $"{ruleName}: missing {sourceTypeFullName}");
        Assert.True(targetType is not null, $"{ruleName}: missing {targetTypeFullName}");

        var op = sourceType!.GetMethod(
            "op_Explicit",
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types: [sourceType],
            modifiers: null
        );

        Assert.True(
            op is not null && op.ReturnType == targetType,
            $"{ruleName}: expected public static {targetType!.FullName} op_Explicit({sourceType.FullName}) on {sourceType.FullName}"
        );
    }

    private static Type? ResolveApplicationOrDomainType(string fullName) =>
        ArchitectureAssemblies.Application.GetType(fullName)
        ?? ArchitectureAssemblies.Domain.GetType(fullName);
}
