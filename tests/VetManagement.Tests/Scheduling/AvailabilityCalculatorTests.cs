using FluentAssertions;
using VetManagement.Domain.Enums;
using VetManagement.Domain.Scheduling;

namespace VetManagement.Tests.Scheduling;

public class AvailabilityCalculatorTests
{
    // Thursday 2026-10-01 12:00 UTC; the next Wednesday is 2026-10-07.
    private static readonly DateTime Now = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Wednesday = new(2026, 10, 7);

    private static TimeBlock Block(int startHour, int startMin, int endHour, int endMin, int maxParallel = 1, bool overflow = false)
        => new() { Start = new TimeOnly(startHour, startMin), End = new TimeOnly(endHour, endMin), MaxParallel = maxParallel, IsOverflow = overflow };

    private static SchedulingSettings Settings(int durationMinutes = 30, int bufferMinutes = 0, bool progressive = false, params TimeBlock[] wednesdayBlocks) => new()
    {
        Enabled = true,
        TimeZone = "UTC",
        Booking = new BookingPolicy { MinNoticeMinutes = 60, HorizonDays = 30, SlotStepMinutes = 30 },
        Services = [new ServiceDefinition { Code = "svc", Name = "Service", Enabled = true, DurationMinutes = durationMinutes, BufferMinutes = bufferMinutes, ResourceCodes = ["vet"] }],
        Resources = [new ResourceDefinition
        {
            Code = "vet", Name = "Vet", Kind = ResourceKind.Vet, ProgressiveRelease = progressive,
            Weekly = [new WeeklyAvailability { Day = DayOfWeek.Wednesday, Blocks = wednesdayBlocks.ToList() }]
        }]
    };

    private static IReadOnlyList<AvailableSlot> Slots(SchedulingSettings s, BookingAudience audience = BookingAudience.Public, params BookedInterval[] booked)
        => AvailabilityCalculator.GetSlots(s, "svc", Wednesday, Wednesday, booked, Now, audience);

    private static BookedInterval Booked(int startHour, int startMin, int endHour, int endMin)
        => new("vet", Wednesday.ToDateTime(new TimeOnly(startHour, startMin), DateTimeKind.Utc), Wednesday.ToDateTime(new TimeOnly(endHour, endMin), DateTimeKind.Utc));

    private static IEnumerable<string> Times(IEnumerable<AvailableSlot> slots) => slots.Select(s => s.StartUtc.ToString("HH:mm"));

    [Fact]
    public void Slots_StepThroughTheBlock_AndMustFitTheDuration()
    {
        var slots = Slots(Settings(durationMinutes: 45, wednesdayBlocks: Block(9, 0, 11, 0)));

        Times(slots).Should().Equal("09:00", "09:30", "10:00"); // 10:30 + 45 min would end after 11:00
        slots[0].EndUtc.Should().Be(slots[0].StartUtc.AddMinutes(45));
    }

    [Fact]
    public void BookedTime_AndBuffer_BlockOverlappingSlots()
    {
        var slots = Slots(Settings(bufferMinutes: 30, wednesdayBlocks: Block(9, 0, 12, 0)), BookingAudience.Public, Booked(10, 0, 11, 0));

        // A 09:30 slot would need 09:30-10:30 (30 min + 30 min buffer) and collide with 10:00.
        Times(slots).Should().Equal("09:00", "11:00", "11:30");
    }

    [Fact]
    public void MaxParallel_AllowsOverlappingBookings_UpToCapacity()
    {
        var settings = Settings(wednesdayBlocks: Block(9, 0, 10, 0, maxParallel: 2));

        Times(Slots(settings, BookingAudience.Public, Booked(9, 0, 9, 30))).Should().Contain("09:00");
        Times(Slots(settings, BookingAudience.Public, Booked(9, 0, 9, 30), Booked(9, 0, 9, 30))).Should().NotContain("09:00");
    }

    [Fact]
    public void DisabledService_HasNoSlots_ForAnyone()
    {
        var settings = Settings(wednesdayBlocks: Block(9, 0, 10, 0));
        settings.Services[0].Enabled = false;

        Slots(settings, BookingAudience.Public).Should().BeEmpty();
        Slots(settings, BookingAudience.Staff).Should().BeEmpty();
    }

    [Fact]
    public void DisabledAgenda_OrOfflineService_HidesSlotsFromPublic_ButNotFromStaff()
    {
        var agendaOff = Settings(wednesdayBlocks: Block(9, 0, 10, 0));
        agendaOff.Enabled = false;
        var offline = Settings(wednesdayBlocks: Block(9, 0, 10, 0));
        offline.Services[0].AllowOnlineBooking = false;

        Slots(agendaOff, BookingAudience.Public).Should().BeEmpty();
        Slots(agendaOff, BookingAudience.Staff).Should().NotBeEmpty();
        Slots(offline, BookingAudience.Public).Should().BeEmpty();
        Slots(offline, BookingAudience.Staff).Should().NotBeEmpty();
    }

    [Theory]
    [InlineData(null, ScheduleExceptionKind.Closed)]   // holiday for the whole clinic
    [InlineData("vet", ScheduleExceptionKind.Closed)]
    [InlineData("vet", ScheduleExceptionKind.Absence)] // vet absent
    public void ClosedAndAbsenceExceptions_RemoveTheDay(string? resourceCode, ScheduleExceptionKind kind)
    {
        var settings = Settings(wednesdayBlocks: Block(9, 0, 12, 0));
        settings.Exceptions.Add(new ScheduleException { From = Wednesday, To = Wednesday, ResourceCode = resourceCode, Kind = kind });

        Slots(settings, BookingAudience.Staff).Should().BeEmpty();
    }

    [Fact]
    public void CustomHours_ReplaceTheWeeklyBlocks()
    {
        var settings = Settings(wednesdayBlocks: Block(9, 0, 12, 0));
        settings.Exceptions.Add(new ScheduleException
        {
            From = Wednesday, To = Wednesday, ResourceCode = "vet", Kind = ScheduleExceptionKind.CustomHours, Blocks = [Block(15, 0, 16, 0)]
        });

        Times(Slots(settings)).Should().Equal("15:00", "15:30");
    }

    [Fact]
    public void MinimumNotice_AndHorizon_ApplyToPublicOnly()
    {
        var settings = Settings(wednesdayBlocks: Block(9, 0, 10, 0));
        var today = new DateTime(2026, 10, 7, 9, 10, 0, DateTimeKind.Utc); // 9:10 on the Wednesday itself
        settings.Booking.MinNoticeMinutes = 30;

        var publicSlots = AvailabilityCalculator.GetSlots(settings, "svc", Wednesday, Wednesday, [], today, BookingAudience.Public);
        var staffSlots = AvailabilityCalculator.GetSlots(settings, "svc", Wednesday, Wednesday, [], today, BookingAudience.Staff);
        Times(publicSlots).Should().BeEmpty("09:30 is within the 30 min notice");
        Times(staffSlots).Should().Equal("09:30");

        settings.Booking.HorizonDays = 3; // Wednesday is 6 days after Now
        Slots(settings, BookingAudience.Public).Should().BeEmpty();
        Slots(settings, BookingAudience.Staff).Should().NotBeEmpty();
    }

    // The clinic's real case: specialist 10:00-17:00. Without release, two clients pick 10:00 and 16:15.
    private static SchedulingSettings Specialist() =>
        Settings(durationMinutes: 45, progressive: true, wednesdayBlocks: [Block(10, 0, 13, 0), Block(13, 30, 17, 0, overflow: true)]);

    [Fact]
    public void ProgressiveRelease_PublicSeesOnlyTheFirstBlock_StaffSeesAll()
    {
        var publicSlots = Slots(Specialist(), BookingAudience.Public);
        var staffSlots = Slots(Specialist(), BookingAudience.Staff);

        publicSlots.Should().OnlyContain(s => s.StartUtc.Hour < 13);
        staffSlots.Should().Contain(s => s.StartUtc.Hour >= 13 && s.IsOverflow);
    }

    [Fact]
    public void ProgressiveRelease_OpensTheOverflowBlock_WhenTheFirstIsFull()
    {
        var full = new[] { Booked(10, 0, 10, 45), Booked(10, 45, 11, 30), Booked(11, 30, 12, 15), Booked(12, 15, 13, 0) };

        var slots = Slots(Specialist(), BookingAudience.Public, full);

        slots.Should().NotBeEmpty().And.OnlyContain(s => s.IsOverflow && s.StartUtc >= Wednesday.ToDateTime(new TimeOnly(13, 30), DateTimeKind.Utc));
    }

    [Fact]
    public void ProgressiveRelease_OpensNextBlock_WhenOnlyAnUnusableGapIsLeft()
    {
        // 30 free minutes remain (10:00-10:30) but the service needs 45: the block can't take more bookings.
        var bookings = new[] { Booked(10, 30, 11, 15), Booked(11, 15, 12, 0), Booked(12, 0, 12, 45) };

        var slots = Slots(Specialist(), BookingAudience.Public, bookings);

        slots.Should().Contain(s => s.IsOverflow);
    }

    [Fact]
    public void ProgressiveRelease_OpensNextBlock_WhenTheFirstIsAlreadyInThePast()
    {
        var lateMorning = new DateTime(2026, 10, 7, 12, 50, 0, DateTimeKind.Utc);
        var settings = Specialist();
        settings.Booking.MinNoticeMinutes = 0;

        var slots = AvailabilityCalculator.GetSlots(settings, "svc", Wednesday, Wednesday, [], lateMorning, BookingAudience.Public);

        slots.Should().NotBeEmpty().And.OnlyContain(s => s.IsOverflow);
    }

    [Fact]
    public void ProgressiveRelease_ThresholdBelow100_OpensEarlier()
    {
        var settings = Specialist();
        settings.Resources[0].ReleaseThresholdPercent = 50; // 90 of 180 minutes booked is enough
        var half = new[] { Booked(10, 0, 10, 45), Booked(10, 45, 11, 30) };

        Slots(settings, BookingAudience.Public, half).Should().Contain(s => s.IsOverflow);
    }

    [Fact]
    public void LocalTimes_AreConvertedToUtc_AcrossChileanDaylightSaving()
    {
        var settings = Settings(wednesdayBlocks: Block(10, 0, 10, 30));
        settings.TimeZone = "America/Santiago";
        settings.Booking.HorizonDays = 400;

        var winter = AvailabilityCalculator.GetSlots(settings, "svc", new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 1), [], new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc), BookingAudience.Public);
        var summer = AvailabilityCalculator.GetSlots(settings, "svc", new DateOnly(2027, 1, 6), new DateOnly(2027, 1, 6), [], Now, BookingAudience.Public);

        winter.Single().StartUtc.Should().Be(new DateTime(2026, 7, 1, 14, 0, 0, DateTimeKind.Utc)); // UTC-4
        summer.Single().StartUtc.Should().Be(new DateTime(2027, 1, 6, 13, 0, 0, DateTimeKind.Utc)); // UTC-3
    }
}
