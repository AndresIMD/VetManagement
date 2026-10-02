using VetManagement.Application.Scheduling;

namespace VetManagement.Api.Services;

/// <summary>Every few minutes: emails reminders for upcoming appointments (when enabled) and releases online bookings whose payment hold expired.</summary>
public class AppointmentRemindersHostedService(IServiceScopeFactory scopeFactory, ILogger<AppointmentRemindersHostedService> logger)
    : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var sent = await scope.ServiceProvider.GetRequiredService<AppointmentService>().SendDueRemindersAsync();
                if (sent > 0)
                    logger.LogInformation("Sent {Count} appointment reminder(s)", sent);
                var released = await scope.ServiceProvider.GetRequiredService<OnlineBookingService>().ExpireUnpaidHoldsAsync();
                if (released > 0)
                    logger.LogInformation("Released {Count} online booking(s) whose payment hold expired", released);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error sending appointment reminders");
            }
        }
    }
}
