using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VetManagement.Api.Authorization;
using VetManagement.Application.Scheduling;
using VetManagement.Contracts.Scheduling;
using VetManagement.Domain.Enums;
using VetManagement.Infrastructure.Data;

namespace VetManagement.Tests.Integration;

/// <summary>
/// Public booking portal flow against the API with the simulated payment provider: booking without an account,
/// deposit payment round trip, client cancellation and the clinic's refund policy.
/// </summary>
public class OnlineBookingTests : IClassFixture<CustomWebAppFactory>
{
    private readonly CustomWebAppFactory _factory;
    private readonly HttpClient _admin;
    private readonly HttpClient _public;
    private static readonly SemaphoreSlim Serial = new(1, 1);
    private static int _rutSeed = 15_000_000;

    public OnlineBookingTests(CustomWebAppFactory factory)
    {
        _factory = factory;
        _admin = factory.CreateClientWithPermissions([.. Permissions.ForRole("Admin")]);
        _public = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    private static string NewRut()
    {
        var number = Interlocked.Increment(ref _rutSeed);
        return $"{number}-{VetManagement.Domain.Clients.Rut.ComputeCheckDigit(number)}";
    }

    private static DateOnly Next(DayOfWeek day, int weeksAhead)
    {
        var date = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(14 + 7 * weeksAhead);
        while (date.DayOfWeek != day)
            date = date.AddDays(1);
        return date;
    }

    private async Task ConfigureAsync(RefundMode refundMode = RefundMode.Automatic, int cancelDeadlineHours = 24)
    {
        var doc = (await _admin.GetFromJsonAsync<JsonObject>("/api/scheduling/settings"))!;
        var s = doc["settings"]!;
        s["enabled"] = true;
        s["clinicName"] = "San Pablo Vet Clinic";
        s["cancellation"] = new JsonObject { ["clientDeadlineHours"] = cancelDeadlineHours, ["refundMode"] = refundMode.ToString() };
        s["booking"]!["horizonDays"] = 180; // tests book up to ~10 weeks ahead
        foreach (var service in s["services"]!.AsArray())
            service!["enabled"] = service["code"]!.GetValue<string>() is "consultation" or "specialist";
        (await _admin.PutAsJsonAsync("/api/scheduling/settings", doc)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private async Task<List<AvailableSlotDto>> PublicSlotsAsync(string service, DateOnly date)
        => (await _public.GetFromJsonAsync<List<AvailableSlotDto>>($"/api/public/booking/availability?service={service}&from={date:yyyy-MM-dd}&to={date:yyyy-MM-dd}"))!;

    private Task<HttpResponseMessage> BookAsync(string service, AvailableSlotDto slot, string rut, string email = "cliente@mail.cl", string pet = "Luna")
        => _public.PostAsJsonAsync("/api/public/booking", new
        {
            ServiceCode = service, slot.ResourceCode, slot.StartUtc,
            OwnerFirstName = "Ana", OwnerLastName = "Lopez", OwnerTaxId = rut, OwnerEmail = email,
            OwnerPhone = "+56 9 1234 5678", PetName = pet, PetSpecies = Species.Cat
        });

    /// <summary>Plays the browser: simulated bank page decision, then the provider's redirect back to the API.</summary>
    private async Task<string> PayAsync(OnlineBookingResponseDto booking, bool approve)
    {
        var decision = await _public.PostAsync($"{booking.PaymentUrl}/{(approve ? "approve" : "reject")}", null);
        decision.StatusCode.Should().Be(HttpStatusCode.Redirect);
        var back = await _public.GetAsync(decision.Headers.Location);
        back.StatusCode.Should().Be(HttpStatusCode.Redirect);
        return back.Headers.Location!.ToString();
    }

    private async Task<PublicBookingDto> PublicBookingAsync(string token) => (await _public.GetFromJsonAsync<PublicBookingDto>($"/api/public/booking/{token}"))!;

    [Fact]
    public async Task NoDepositService_IsConfirmedRightAway_AndCreatesTheClientAndPet_Once()
    {
        await Serial.WaitAsync();
        try
        {
            await ConfigureAsync();
            var rut = NewRut();
            var slots = await PublicSlotsAsync("consultation", Next(DayOfWeek.Monday, 0));

            var first = (await (await BookAsync("consultation", slots[0], rut.Replace("-", ""), "first@mail.cl")).Content.ReadFromJsonAsync<OnlineBookingResponseDto>())!;
            var later = slots.First(s => s.StartUtc >= slots[0].EndUtc);
            var second = (await (await BookAsync("consultation", later, rut, "first@mail.cl")).Content.ReadFromJsonAsync<OnlineBookingResponseDto>())!;

            first.Confirmed.Should().BeTrue();
            first.PaymentUrl.Should().BeNull();
            (await PublicBookingAsync(first.PublicToken)).Status.Should().Be(AppointmentStatus.Confirmed);
            _factory.Emails.Sent.Should().Contain(e => e.To == "first@mail.cl" && e.Subject.StartsWith("Confirmación"));

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var clients = await db.Clients.Include(c => c.Pets).Where(c => c.TaxId == rut).ToListAsync();
            clients.Should().ContainSingle("the same RUT is matched to the same client").Which.Pets.Should().ContainSingle(p => p.Name == "Luna");
            var appointments = await db.Appointments.Where(a => a.PublicToken == first.PublicToken || a.PublicToken == second.PublicToken).ToListAsync();
            appointments.Should().HaveCount(2).And.OnlyContain(a => a.ClientId == clients[0].Id && a.Source == AppointmentSource.Online);
        }
        finally { Serial.Release(); }
    }

    [Theory]
    [InlineData("12.345.678-4", "ok@mail.cl")]   // wrong RUT check digit
    [InlineData("12.345.678-5", "not-an-email")]
    public async Task InvalidClientData_IsRejected(string rut, string email)
    {
        await Serial.WaitAsync();
        try
        {
            await ConfigureAsync();
            var slot = (await PublicSlotsAsync("consultation", Next(DayOfWeek.Tuesday, 0)))[0];

            (await BookAsync("consultation", slot, rut, email)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }
        finally { Serial.Release(); }
    }

    [Fact]
    public async Task DepositService_HoldsTheSlot_UntilPaid_ThenConfirms()
    {
        await Serial.WaitAsync();
        try
        {
            await ConfigureAsync();
            var date = Next(DayOfWeek.Wednesday, 1);
            var slot = (await PublicSlotsAsync("specialist", date))[0];

            var booking = (await (await BookAsync("specialist", slot, NewRut(), "paid@mail.cl")).Content.ReadFromJsonAsync<OnlineBookingResponseDto>())!;
            booking.Confirmed.Should().BeFalse();
            booking.PaymentUrl.Should().Contain("/api/public/payments/simulated/");
            (await PublicBookingAsync(booking.PublicToken)).Status.Should().Be(AppointmentStatus.PendingPayment);
            (await PublicSlotsAsync("specialist", date)).Should().NotContain(s => s.StartUtc == slot.StartUtc, "the slot is held while paying");

            var landing = await PayAsync(booking, approve: true);

            landing.Should().EndWith($"/booking/{booking.PublicToken}");
            var paid = await PublicBookingAsync(booking.PublicToken);
            paid.Status.Should().Be(AppointmentStatus.Confirmed);
            paid.DepositStatus.Should().Be(DepositStatus.Paid);
            _factory.Emails.Sent.Should().Contain(e => e.To == "paid@mail.cl" && e.Subject.StartsWith("Confirmación"));
        }
        finally { Serial.Release(); }
    }

    [Fact]
    public async Task RejectedOrAbortedPayment_ReleasesTheSlot()
    {
        await Serial.WaitAsync();
        try
        {
            await ConfigureAsync();
            var date = Next(DayOfWeek.Wednesday, 2);
            var slot = (await PublicSlotsAsync("specialist", date))[0];

            var rejected = (await (await BookAsync("specialist", slot, NewRut())).Content.ReadFromJsonAsync<OnlineBookingResponseDto>())!;
            await PayAsync(rejected, approve: false);
            (await PublicBookingAsync(rejected.PublicToken)).Status.Should().Be(AppointmentStatus.Cancelled);
            (await PublicSlotsAsync("specialist", date)).Should().Contain(s => s.StartUtc == slot.StartUtc);

            // WebPay sends TBK_TOKEN (no token_ws) when the client aborts on the bank page.
            var aborted = (await (await BookAsync("specialist", slot, NewRut())).Content.ReadFromJsonAsync<OnlineBookingResponseDto>())!;
            var token = aborted.PaymentUrl!.Split('/').Last();
            (await _public.GetAsync($"/api/public/booking/payment-return?TBK_TOKEN={token}")).StatusCode.Should().Be(HttpStatusCode.Redirect);
            (await PublicBookingAsync(aborted.PublicToken)).Status.Should().Be(AppointmentStatus.Cancelled);
        }
        finally { Serial.Release(); }
    }

    [Fact]
    public async Task PublicAvailability_UsesProgressiveRelease()
    {
        await Serial.WaitAsync();
        try
        {
            await ConfigureAsync();
            var date = Next(DayOfWeek.Wednesday, 3);

            var slots = await PublicSlotsAsync("specialist", date);

            slots.Should().NotBeEmpty().And.OnlyContain(s => !s.IsOverflow, "the overflow afternoon opens only when the morning is full");
        }
        finally { Serial.Release(); }
    }

    [Theory]
    [InlineData(RefundMode.Automatic, DepositStatus.Refunded)]
    [InlineData(RefundMode.NoRefund, DepositStatus.Forfeited)]
    [InlineData(RefundMode.ManualApproval, DepositStatus.RefundRequested)]
    public async Task ClientCancellation_FollowsTheClinicsRefundMode(RefundMode mode, DepositStatus expected)
    {
        await Serial.WaitAsync();
        try
        {
            await ConfigureAsync(refundMode: mode);
            var date = Next(DayOfWeek.Wednesday, 4 + (int)mode);
            var slot = (await PublicSlotsAsync("specialist", date))[0];
            var booking = (await (await BookAsync("specialist", slot, NewRut())).Content.ReadFromJsonAsync<OnlineBookingResponseDto>())!;
            await PayAsync(booking, approve: true);

            (await _public.PostAsync($"/api/public/booking/{booking.PublicToken}/cancel", null)).StatusCode.Should().Be(HttpStatusCode.OK);

            var cancelled = await PublicBookingAsync(booking.PublicToken);
            cancelled.Status.Should().Be(AppointmentStatus.Cancelled);
            cancelled.DepositStatus.Should().Be(expected);
        }
        finally { Serial.Release(); }
    }

    [Fact]
    public async Task ClientCancellation_AfterTheDeadline_IsRefused()
    {
        await Serial.WaitAsync();
        try
        {
            await ConfigureAsync(cancelDeadlineHours: 24 * 400); // every future booking is already past the deadline
            var slot = (await PublicSlotsAsync("consultation", Next(DayOfWeek.Thursday, 0)))[0];
            var booking = (await (await BookAsync("consultation", slot, NewRut())).Content.ReadFromJsonAsync<OnlineBookingResponseDto>())!;

            (await _public.PostAsync($"/api/public/booking/{booking.PublicToken}/cancel", null)).StatusCode.Should().Be(HttpStatusCode.Conflict);
            (await PublicBookingAsync(booking.PublicToken)).CanCancel.Should().BeFalse();
        }
        finally { Serial.Release(); }
    }

    [Fact]
    public async Task UnpaidHolds_AreReleased_WhenTheyExpire()
    {
        await Serial.WaitAsync();
        try
        {
            await ConfigureAsync();
            var slot = (await PublicSlotsAsync("specialist", Next(DayOfWeek.Wednesday, 8)))[0];
            var booking = (await (await BookAsync("specialist", slot, NewRut())).Content.ReadFromJsonAsync<OnlineBookingResponseDto>())!;

            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var appointment = await db.Appointments.SingleAsync(a => a.PublicToken == booking.PublicToken);
                appointment.PaymentHoldUntilUtc = DateTime.UtcNow.AddMinutes(-1); // the client never came back
                await db.SaveChangesAsync();
            }
            using (var scope = _factory.Services.CreateScope())
                (await scope.ServiceProvider.GetRequiredService<OnlineBookingService>().ExpireUnpaidHoldsAsync()).Should().BeGreaterThanOrEqualTo(1);

            (await PublicBookingAsync(booking.PublicToken)).Status.Should().Be(AppointmentStatus.Cancelled);
        }
        finally { Serial.Release(); }
    }
}
