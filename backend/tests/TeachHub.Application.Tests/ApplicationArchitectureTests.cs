using Shouldly;
using TeachHub.Application.Common;

namespace TeachHub.Application.Tests;

public class ApplicationArchitectureTests
{
    [Fact]
    public void Application_must_not_reference_ASP_NET_Core()
    {
        // ArchitectureTests over in Domain.Tests checks ONLY the Domain assembly. That leaves the
        // Application layer free to pull in Microsoft.AspNetCore.Http without a single test going
        // red — and that is exactly how IFormFile drifts down into Application, turning every
        // handler test into an exercise in faking HTTP.
        // The convention: commands take a Stream plus a file name plus a length; IFormFile appears
        // only inside Controllers/.
        //
        // Microsoft.AspNetCore.Identity is NOT on the forbidden list: UserManager/RoleManager
        // actually come from Microsoft.Extensions.Identity.Core, plain POCOs that drag in no web
        // infrastructure.
        string[] forbidden =
        [
            "Microsoft.AspNetCore.Http",
            "Microsoft.AspNetCore.Mvc",
            "Microsoft.AspNetCore.Server",
            "Hangfire",
            "AWSSDK",
            "ClosedXML",
        ];

        var referenced = typeof(Result).Assembly
            .GetReferencedAssemblies()
            .Select(a => a.Name!)
            .ToList();

        foreach (var f in forbidden)
            referenced.ShouldNotContain(n => n.StartsWith(f, StringComparison.Ordinal),
                $"Application references {f} — the business layer must not know infrastructure exists.");
    }
}
