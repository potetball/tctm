using TCTM.Server.Services;

namespace TCTM.Server.Services;

/// <summary>
/// Background service that periodically checks for clock timeouts
/// and broadcasts clock updates to active game groups.
/// </summary>
public class ClockMonitorService(IServiceProvider serviceProvider, ILogger<ClockMonitorService> logger) : BackgroundService
{
    /// <summary>Interval for timeout checks (1 second).</summary>
    private static readonly TimeSpan TimeoutCheckInterval = TimeSpan.FromSeconds(1);

    /// <summary>Interval for clock broadcasts (5 seconds).</summary>
    private static readonly TimeSpan ClockBroadcastInterval = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("ClockMonitorService started.");

        var lastBroadcast = DateTimeOffset.UtcNow;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = serviceProvider.CreateScope();
                var gameService = scope.ServiceProvider.GetRequiredService<LiveGameService>();

                // Check timeouts every second
                await gameService.CheckTimeoutsAsync();

                // Broadcast clock updates every 5 seconds
                if (DateTimeOffset.UtcNow - lastBroadcast >= ClockBroadcastInterval)
                {
                    await gameService.BroadcastClockUpdatesAsync();
                    lastBroadcast = DateTimeOffset.UtcNow;
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in ClockMonitorService loop.");
            }

            await Task.Delay(TimeoutCheckInterval, stoppingToken);
        }

        logger.LogInformation("ClockMonitorService stopped.");
    }
}
