using NetArchTest.Rules;

namespace PointLurker.Architecture.Tests;

public class LayerDependencyTests
{
    [Fact]
    public void Domain_must_not_depend_on_EntityFrameworkCore()
    {
        var result = Types
            .InAssembly(ArchitectureAssemblies.Domain)
            .Should()
            .NotHaveDependencyOnAny("Microsoft.EntityFrameworkCore", "Npgsql")
            .GetResult();

        Assert.True(
            result.IsSuccessful,
            ArchAssert.Format(nameof(Domain_must_not_depend_on_EntityFrameworkCore), result)
        );
    }

    [Fact]
    public void Domain_must_not_depend_on_Refit()
    {
        var result = Types
            .InAssembly(ArchitectureAssemblies.Domain)
            .Should()
            .NotHaveDependencyOn("Refit")
            .GetResult();

        Assert.True(
            result.IsSuccessful,
            ArchAssert.Format(nameof(Domain_must_not_depend_on_Refit), result)
        );
    }

    [Fact]
    public void Domain_must_not_depend_on_MailKit_or_Cronos()
    {
        var result = Types
            .InAssembly(ArchitectureAssemblies.Domain)
            .Should()
            .NotHaveDependencyOnAny("MailKit", "MimeKit", "Cronos")
            .GetResult();

        Assert.True(
            result.IsSuccessful,
            ArchAssert.Format(nameof(Domain_must_not_depend_on_MailKit_or_Cronos), result)
        );
    }

    [Fact]
    public void Domain_may_only_use_Identity_AspNet_namespaces()
    {
        // Forbid all known Microsoft.AspNetCore.* sub-namespaces except Identity*.
        // Identity primitives live in Microsoft.AspNetCore.Identity and Microsoft.AspNetCore.Identity.EntityFrameworkCore.
        var forbidden = new[]
        {
            "Microsoft.AspNetCore.Mvc",
            "Microsoft.AspNetCore.Http",
            "Microsoft.AspNetCore.Authentication",
            "Microsoft.AspNetCore.Authorization",
            "Microsoft.AspNetCore.Builder",
            "Microsoft.AspNetCore.Hosting",
            "Microsoft.AspNetCore.Routing",
            "Microsoft.AspNetCore.OpenApi",
            "Microsoft.AspNetCore.SpaProxy",
            "Microsoft.AspNetCore.DataProtection",
        };

        var result = Types
            .InAssembly(ArchitectureAssemblies.Domain)
            .Should()
            .NotHaveDependencyOnAny(forbidden)
            .GetResult();

        Assert.True(
            result.IsSuccessful,
            ArchAssert.Format(nameof(Domain_may_only_use_Identity_AspNet_namespaces), result)
        );
    }

    [Fact]
    public void Application_must_not_depend_on_Infrastructure()
    {
        var result = Types
            .InAssembly(ArchitectureAssemblies.Application)
            .Should()
            .NotHaveDependencyOnAny(
                "PointLurker.Infrastructure.Database",
                "PointLurker.Infrastructure.ExternalServices",
                "Microsoft.EntityFrameworkCore",
                "Npgsql",
                "Refit",
                "MailKit",
                "MimeKit"
            )
            .GetResult();

        Assert.True(
            result.IsSuccessful,
            ArchAssert.Format(nameof(Application_must_not_depend_on_Infrastructure), result)
        );
    }

    [Fact]
    public void Infrastructure_Database_must_not_depend_on_ExternalServices()
    {
        var result = Types
            .InAssembly(ArchitectureAssemblies.InfrastructureDatabase)
            .Should()
            .NotHaveDependencyOn("PointLurker.Infrastructure.ExternalServices")
            .GetResult();

        Assert.True(
            result.IsSuccessful,
            ArchAssert.Format(
                nameof(Infrastructure_Database_must_not_depend_on_ExternalServices),
                result
            )
        );
    }

    [Fact]
    public void Infrastructure_ExternalServices_must_not_depend_on_Database()
    {
        var result = Types
            .InAssembly(ArchitectureAssemblies.InfrastructureExternalServices)
            .Should()
            .NotHaveDependencyOn("PointLurker.Infrastructure.Database")
            .GetResult();

        Assert.True(
            result.IsSuccessful,
            ArchAssert.Format(
                nameof(Infrastructure_ExternalServices_must_not_depend_on_Database),
                result
            )
        );
    }
}
