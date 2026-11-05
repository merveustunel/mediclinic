using MediClinic.Domain;

namespace MediClinic.Tests;

public class PersonTests
{
    [Fact]
    public void InheritanceSupportsPolymorphicRoles()
    {
        var fixture = new DomainFixture();
        Person[] people = [fixture.Patient,
            new Staff(Guid.NewGuid(), "Sample", "Staff", fixture.Department, "Receptionist"), fixture.Doctor];

        Assert.True(typeof(Person).IsAbstract);
        Assert.IsAssignableFrom<Staff>(fixture.Doctor);
        Assert.Equal(new[] { "Patient", "Receptionist", "Doctor" }, people.Select(person => person.Role));
        Assert.Equal("General medicine", fixture.Doctor.Specialty);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NamesAreRequiredAndFailedRenameDoesNotMutate(string? invalid)
    {
        var patient = new DomainFixture().Patient;
        Assert.ThrowsAny<ArgumentException>(() => new Patient(Guid.NewGuid(), invalid!, "Patient", patient.DateOfBirth));
        Assert.ThrowsAny<ArgumentException>(() => new Patient(Guid.NewGuid(), "Sample", invalid!, patient.DateOfBirth));
        Assert.ThrowsAny<ArgumentException>(() => patient.Rename("Changed", invalid!));
        Assert.Equal("Sample Patient", patient.FullName);
    }

    [Fact]
    public void RenameTrimsBothNames()
    {
        var patient = new DomainFixture().Patient;
        patient.Rename(" Updated ", " Name ");
        Assert.Equal("Updated Name", patient.FullName);
    }

    [Fact]
    public void PersonRejectsEmptyIdentityAndFutureBirthDate()
    {
        Assert.Throws<ArgumentException>(() => new Patient(Guid.Empty, "Sample", "Patient", new DateOnly(2000, 1, 1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Patient(Guid.NewGuid(), "Sample", "Patient", DateOnly.MaxValue));
    }

    [Fact]
    public void StaffRequiresDepartmentAndValidatedRoleAndSpecialty()
    {
        var department = new DomainFixture().Department;
        Assert.Throws<ArgumentNullException>(() => new Staff(Guid.NewGuid(), "Sample", "Staff", null!, "Receptionist"));
        Assert.Throws<ArgumentException>(() => new Staff(Guid.NewGuid(), "Sample", "Staff", department, " "));
        Assert.Throws<ArgumentException>(() => new Doctor(Guid.NewGuid(), "Sample", "Doctor", department, " "));
    }
}
