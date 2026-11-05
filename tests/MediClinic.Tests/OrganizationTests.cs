using MediClinic.Domain;

namespace MediClinic.Tests;

public class OrganizationTests
{
    [Fact]
    public void CompositionKeepsParentRelationshipsAndIdsConsistent()
    {
        var fixture = new DomainFixture();
        var branch = Assert.Single(fixture.Clinic.Branches);
        Assert.Same(fixture.Clinic, branch.Clinic);
        Assert.Equal(fixture.Clinic.Id, branch.ClinicId);
        Assert.Same(branch, fixture.Department.Branch);
        Assert.Equal(branch.Id, fixture.Department.BranchId);
        Assert.Same(fixture.Department, Assert.Single(branch.Departments));
        Assert.Same(fixture.Department, fixture.Service.Department);
        Assert.Equal(fixture.Department.Id, fixture.Service.DepartmentId);
        Assert.Same(fixture.Service, Assert.Single(fixture.Department.Services));
        Assert.Equal(fixture.Department.Id, fixture.Doctor.DepartmentId);
    }

    [Fact]
    public void ChildrenCannotBeInsertedThroughReadOnlyCollections()
    {
        var fixture = new DomainFixture();
        Assert.Throws<NotSupportedException>(() => ((ICollection<Branch>)fixture.Clinic.Branches).Clear());
        Assert.Throws<NotSupportedException>(() => ((ICollection<Department>)fixture.Department.Branch.Departments).Clear());
        Assert.Throws<NotSupportedException>(() => ((ICollection<MedicalService>)fixture.Department.Services).Clear());
    }

    [Fact]
    public void DuplicateChildIdsAreRejectedWithoutChangingCollections()
    {
        var fixture = new DomainFixture();
        var branch = fixture.Department.Branch;
        Assert.Throws<InvalidOperationException>(() => fixture.Clinic.AddBranch(branch.Id, "Other", "Address"));
        Assert.Throws<InvalidOperationException>(() => branch.AddDepartment(fixture.Department.Id, "Other"));
        Assert.Throws<InvalidOperationException>(() => fixture.Department.AddService(fixture.Service.Id, "Other", TimeSpan.FromMinutes(15)));
        Assert.Single(fixture.Clinic.Branches);
        Assert.Single(branch.Departments);
        Assert.Single(fixture.Department.Services);
    }

    [Fact]
    public void InvalidChildrenAreNotAdded()
    {
        var fixture = new DomainFixture();
        Assert.Throws<ArgumentException>(() => new Clinic(Guid.Empty, "Clinic"));
        Assert.Throws<ArgumentException>(() => new Clinic(Guid.NewGuid(), " "));
        Assert.Throws<ArgumentException>(() => fixture.Clinic.AddBranch(Guid.Empty, "Branch", "Address"));
        Assert.Throws<ArgumentException>(() => fixture.Clinic.AddBranch(Guid.NewGuid(), " ", "Address"));
        Assert.Throws<ArgumentException>(() => fixture.Clinic.AddBranch(Guid.NewGuid(), "Branch", " "));
        Assert.Throws<ArgumentException>(() => fixture.Department.Branch.AddDepartment(Guid.Empty, "Department"));
        Assert.Throws<ArgumentException>(() => fixture.Department.Branch.AddDepartment(Guid.NewGuid(), " "));
        Assert.Throws<ArgumentException>(() => fixture.Department.AddService(Guid.Empty, "Service", TimeSpan.FromMinutes(15)));
        Assert.Throws<ArgumentException>(() => fixture.Department.AddService(Guid.NewGuid(), " ", TimeSpan.FromMinutes(15)));
        Assert.Single(fixture.Clinic.Branches);
        Assert.Single(fixture.Department.Branch.Departments);
        Assert.Single(fixture.Department.Services);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ServiceDurationMustBePositive(int minutes)
    {
        var fixture = new DomainFixture();
        Assert.Throws<ArgumentOutOfRangeException>(() => fixture.Department.AddService(Guid.NewGuid(), "Service", TimeSpan.FromMinutes(minutes)));
        Assert.Single(fixture.Department.Services);
    }
}
