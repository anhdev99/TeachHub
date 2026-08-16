using Shouldly;

namespace TeachHub.Domain.Tests;

public class ArchitectureTests
{
    [Fact]
    public void Domain_must_not_depend_on_infrastructure()
    {
        // MediatR and FluentValidation are on the forbidden list for the same reason as EF Core:
        // ICommand/IQuery are contracts belonging to the Application layer, and the Domain must not
        // know they exist. Without a guard here, the temptation to "let the Domain publish an
        // INotification as a domain event" will win, and business rules lose the ability to be
        // tested in milliseconds — the most expensive thing this architecture buys us.
        string[] forbidden = ["Microsoft.EntityFrameworkCore", "Npgsql", "Hangfire",
                              "MediatR", "FluentValidation",
                              "Microsoft.AspNetCore", "AWSSDK"];

        var referenced = typeof(TeachHub.Domain.Common.Entity).Assembly
            .GetReferencedAssemblies()
            .Select(a => a.Name!)
            .ToList();

        foreach (var f in forbidden)
            referenced.ShouldNotContain(n => n.StartsWith(f, StringComparison.Ordinal),
                $"Domain references {f} — violates ADR-002.");
    }
}
