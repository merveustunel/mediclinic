using System.Reflection;
using MediClinic.Application;
using Microsoft.Extensions.DependencyInjection;

namespace MediClinic.Tests;

public class ScaffoldTests
{
    [Fact]
    public void ApplicationRegistrationSupportsComposition()
    {
        IServiceCollection services = new ServiceCollection();

        Assert.Same(services, services.AddApplication());
    }

    [Fact]
    public void DomainHasNoFrameworkOrOuterLayerDependencies()
    {
        var references = Assembly.Load("MediClinic.Domain").GetReferencedAssemblies();

        Assert.DoesNotContain(references, reference =>
            reference.Name!.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal) ||
            reference.Name.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal) ||
            reference.Name is "MediClinic.Application" or "MediClinic.Infrastructure" or "MediClinic.Web");
    }
}
