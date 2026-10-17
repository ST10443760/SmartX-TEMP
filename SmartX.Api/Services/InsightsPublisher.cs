namespace SmartX.Api.Services;

/// <summary>
/// Re-evaluates the insights panel every few seconds so problem devices are
/// highlighted (and cleared) in real time even when nobody is clicking.
/// Operator actions also publish immediately via <see cref="InsightsService"/>.
/// </summary>
public sealed class InsightsPublisher(InsightsService insights, ILogger<InsightsPublisher> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(3);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await insights.PublishAsync(stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "Failed to publish insights");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Shutdown.
        }
    }
}
