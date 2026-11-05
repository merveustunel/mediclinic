using MediClinic.Domain;

namespace MediClinic.Tests;

public class StaffAvailabilityTests
{
    [Fact]
    public void WindowCoversContainedIntervalsIncludingEqualBoundaries()
    {
        var fixture = new DomainFixture();
        var window = new StaffAvailability(Guid.NewGuid(), fixture.Doctor, fixture.Start, fixture.Start.AddHours(2));
        Assert.Same(fixture.Doctor, window.Staff);
        Assert.Equal(fixture.Doctor.Id, window.StaffId);
        Assert.True(window.Covers(fixture.Start, fixture.Start.AddHours(2)));
        Assert.True(window.Covers(fixture.Start.AddMinutes(15), fixture.Start.AddMinutes(45)));
        Assert.True(window.Covers(fixture.Start.ToOffset(TimeSpan.FromHours(3)), window.EndsAt));
        Assert.False(window.Covers(fixture.Start.AddTicks(-1), fixture.Start.AddMinutes(30)));
        Assert.False(window.Covers(fixture.Start, window.EndsAt.AddTicks(1)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-30)]
    public void InvalidRangesAreRejectedAtConstructionAndQuery(int minutes)
    {
        var fixture = new DomainFixture();
        Assert.Throws<ArgumentException>(() => new StaffAvailability(Guid.NewGuid(), fixture.Doctor, fixture.Start, fixture.Start.AddMinutes(minutes)));
        var window = new StaffAvailability(Guid.NewGuid(), fixture.Doctor, fixture.Start, fixture.Start.AddHours(1));
        Assert.Throws<ArgumentException>(() => window.Covers(fixture.Start, fixture.Start.AddMinutes(minutes)));
    }

    [Fact]
    public void AvailabilityRequiresStaffAndIdentity()
    {
        var fixture = new DomainFixture();
        Assert.Throws<ArgumentNullException>(() => new StaffAvailability(Guid.NewGuid(), null!, fixture.Start, fixture.Start.AddHours(1)));
        Assert.Throws<ArgumentException>(() => new StaffAvailability(Guid.Empty, fixture.Doctor, fixture.Start, fixture.Start.AddHours(1)));
    }
}
