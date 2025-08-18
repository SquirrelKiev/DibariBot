using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace DibariBot.Modules;

[Inject(ServiceLifetime.Singleton)]
public class CustomStatusService(ILogger<CustomStatusService> logger, BotConfig config, DiscordSocketClient client)
{
    private int currentActivityId;

    private Task? timerTask;

    /// <summary>
    /// Starts the background task that refreshes the custom status.
    /// </summary>
    /// <param name="cancellationToken">Indicates that the task should stop refreshing.</param>
    public void StartBackgroundTask(CancellationToken cancellationToken)
    {
        timerTask ??= Task.Run(() => TimerTask(cancellationToken), cancellationToken);
    }

    /// <remarks>Should only be one of these running!</remarks>
    private async Task TimerTask(CancellationToken cancellationToken)
    {
        logger.LogTrace("Custom status timer task started");

        try
        {
            await UpdateStatus();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception in TimerTask! {message}", ex.Message);
        }

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(15));
        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            try
            {
                await UpdateStatus();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unhandled exception in TimerTask! {message}", ex.Message);
            }
        }
    }

    private async Task UpdateStatus()
    {
        if (config.BotActivities.Length == 0 || !config.ShouldHaveActivity)
        {
            await client.SetActivityAsync(null);
            return;
        }

        var activity = config.BotActivities[currentActivityId % config.BotActivities.Length];

        await client.SetStatusAsync(config.UserStatus);

        switch (config.ActivityType)
        {
            case ActivityType.Playing:
            case ActivityType.Listening:
            case ActivityType.Watching:
            case ActivityType.Competing:
                await client.SetActivityAsync(new Game(activity, config.ActivityType));
                break;
            case ActivityType.Streaming:
                await client.SetActivityAsync(new StreamingGame(activity,
                    config.ActivityStreamingUrl));
                break;
            case ActivityType.CustomStatus:
                await client.SetActivityAsync(new CustomStatusGame(activity));
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        logger.LogInformation("Set activity to {activityType}, with contents {activityContents}", config.ActivityType, activity);

        currentActivityId = (currentActivityId + 1) % config.BotActivities.Length;
    }
}