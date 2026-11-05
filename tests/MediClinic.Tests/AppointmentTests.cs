using MediClinic.Domain;

namespace MediClinic.Tests;

public class AppointmentTests
{
    public static IEnumerable<object[]> TransitionCases()
    {
        foreach (var initial in Enum.GetValues<AppointmentStatus>())
        foreach (var target in Enum.GetValues<AppointmentStatus>().Where(status => status != AppointmentStatus.Pending))
        {
            var allowed = (initial, target) is
                (AppointmentStatus.Pending, AppointmentStatus.Confirmed or AppointmentStatus.Rejected or AppointmentStatus.Cancelled) or
                (AppointmentStatus.Confirmed, AppointmentStatus.Cancelled or AppointmentStatus.Completed);
            yield return [initial, target, allowed];
        }
    }

    [Theory]
    [MemberData(nameof(TransitionCases))]
    public void OnlyDocumentedStatusTransitionsAreAllowed(AppointmentStatus initial, AppointmentStatus target, bool allowed)
    {
        var appointment = new DomainFixture().Appointment();
        if (initial == AppointmentStatus.Completed)
        {
            appointment.Confirm();
            appointment.Complete(appointment.EndsAt);
        }
        else if (initial != AppointmentStatus.Pending)
        {
            Transition(appointment, initial);
        }
        var previousReason = appointment.DecisionReason;

        if (allowed)
        {
            Transition(appointment, target);
            Assert.Equal(target, appointment.Status);
        }
        else
        {
            Assert.Throws<InvalidOperationException>(() => Transition(appointment, target));
            Assert.Equal(initial, appointment.Status);
            Assert.Equal(previousReason, appointment.DecisionReason);
        }
    }

    [Fact]
    public void NewAppointmentHasConsistentRelationshipsAndServiceDuration()
    {
        var fixture = new DomainFixture();
        var appointment = fixture.Appointment();
        Assert.Equal(AppointmentStatus.Pending, appointment.Status);
        Assert.Null(appointment.DecisionReason);
        Assert.Same(fixture.Patient, appointment.Patient);
        Assert.Same(fixture.Doctor, appointment.Doctor);
        Assert.Same(fixture.Service, appointment.Service);
        Assert.Equal(fixture.Patient.Id, appointment.PatientId);
        Assert.Equal(fixture.Doctor.Id, appointment.DoctorId);
        Assert.Equal(fixture.Service.Id, appointment.ServiceId);
        Assert.Same(fixture.Department.Branch, appointment.Branch);
        Assert.Equal(fixture.Department.BranchId, appointment.BranchId);
        Assert.Equal(fixture.Start, appointment.StartsAt);
        Assert.Equal(fixture.Start.AddMinutes(30), appointment.EndsAt);
    }

    [Fact]
    public void DoctorCannotProvideServiceFromAnotherDepartment()
    {
        var fixture = new DomainFixture();
        var other = fixture.Department.Branch.AddDepartment(Guid.NewGuid(), "Other");
        var service = other.AddService(Guid.NewGuid(), "Other service", TimeSpan.FromMinutes(15));
        Assert.True(fixture.Doctor.CanProvide(fixture.Service));
        Assert.False(fixture.Doctor.CanProvide(service));
        Assert.Throws<ArgumentException>(() => new Appointment(Guid.NewGuid(), fixture.Patient, fixture.Doctor, service, fixture.Start));
        Assert.Throws<ArgumentNullException>(() => fixture.Doctor.CanProvide(null!));
    }

    [Fact]
    public void AppointmentRequiresIdentityAndAllParticipants()
    {
        var fixture = new DomainFixture();
        Assert.Throws<ArgumentException>(() => new Appointment(Guid.Empty, fixture.Patient, fixture.Doctor, fixture.Service, fixture.Start));
        Assert.Throws<ArgumentNullException>(() => new Appointment(Guid.NewGuid(), null!, fixture.Doctor, fixture.Service, fixture.Start));
        Assert.Throws<ArgumentNullException>(() => new Appointment(Guid.NewGuid(), fixture.Patient, null!, fixture.Service, fixture.Start));
        Assert.Throws<ArgumentNullException>(() => new Appointment(Guid.NewGuid(), fixture.Patient, fixture.Doctor, null!, fixture.Start));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Appointment(Guid.NewGuid(), fixture.Patient, fixture.Doctor, fixture.Service, DateTimeOffset.MaxValue));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void RejectionAndCancellationRequireReasonWithoutMutatingState(string? reason)
    {
        var appointment = new DomainFixture().Appointment();
        Assert.ThrowsAny<ArgumentException>(() => appointment.Reject(reason!));
        Assert.ThrowsAny<ArgumentException>(() => appointment.Cancel(reason!));
        Assert.Equal(AppointmentStatus.Pending, appointment.Status);
        Assert.Null(appointment.DecisionReason);
        appointment.Confirm();
        Assert.ThrowsAny<ArgumentException>(() => appointment.Cancel(reason!));
        Assert.Equal(AppointmentStatus.Confirmed, appointment.Status);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void DecisionReasonsAreRetainedAndTrimmed(bool reject)
    {
        var appointment = new DomainFixture().Appointment();
        if (reject) appointment.Reject(" Unavailable ");
        else appointment.Cancel(" Unavailable ");
        Assert.Equal("Unavailable", appointment.DecisionReason);
    }

    [Fact]
    public void ConfirmedAppointmentCannotCompleteBeforeItsEnd()
    {
        var appointment = new DomainFixture().Appointment();
        appointment.Confirm();
        Assert.Throws<ArgumentOutOfRangeException>(() => appointment.Complete(appointment.EndsAt.AddTicks(-1)));
        Assert.Equal(AppointmentStatus.Confirmed, appointment.Status);
        appointment.Complete(appointment.EndsAt.ToOffset(TimeSpan.FromHours(3)));
        Assert.Equal(AppointmentStatus.Completed, appointment.Status);
    }

    private static void Transition(Appointment appointment, AppointmentStatus target)
    {
        switch (target)
        {
            case AppointmentStatus.Confirmed: appointment.Confirm(); break;
            case AppointmentStatus.Rejected: appointment.Reject("Unavailable"); break;
            case AppointmentStatus.Cancelled: appointment.Cancel("Plans changed"); break;
            case AppointmentStatus.Completed: appointment.Complete(appointment.EndsAt); break;
            default: throw new ArgumentOutOfRangeException(nameof(target));
        }
    }
}
