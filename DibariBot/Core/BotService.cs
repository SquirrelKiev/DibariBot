using System.Reflection;
using DibariBot.Database;
using DibariBot.Modules;
using Discord.Commands;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DibariBot;

public class BotService(
    DiscordSocketClient client,
    BotConfig config,
    DbService dbService,
    ILogger<BotService> logger,
    CommandHandler commandHandler,
    InteractionService interactionService,
    CommandService commandService,
    CustomStatusService customStatusService
) : BackgroundService
{
    private CancellationTokenSource cts = new();
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var args = Environment.GetCommandLineArgs();
        var migrationEnabled = !(args.Contains("nomigrate") || args.Contains("nukedb"));
        await dbService.Initialize(migrationEnabled);

#if DEBUG
        if (Environment.GetCommandLineArgs().Contains("nukedb"))
        {
            logger.LogDebug("Nuking the DB...");

            await dbService.ResetDatabase();

            logger.LogDebug("Nuked!");
        }
#endif

        client.Log += Client_Log;

        client.Ready += Client_Ready;

        interactionService.Log += Client_Log;
        commandService.Log += Client_Log;

        await client.LoginAsync(TokenType.Bot, config.BotToken);
        await client.StartAsync();
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (ExecuteTask == null)
            return;

        await cts.CancelAsync();

        await client.LogoutAsync();
        await client.StopAsync();

        await base.StopAsync(cancellationToken);
    }
    
    private Task Client_Log(LogMessage message)
    {
        return Client_Log(logger, message);
    }
    
    public static Task Client_Log(ILogger logger, LogMessage message)
    {
        var level = message.Severity switch
        {
            LogSeverity.Critical => LogLevel.Critical,
            LogSeverity.Error => LogLevel.Error,
            LogSeverity.Warning => LogLevel.Warning,
            LogSeverity.Info => LogLevel.Information,
            LogSeverity.Verbose => LogLevel.Trace,
            LogSeverity.Debug => LogLevel.Debug,
            _ => LogLevel.Information,
        };

        if (message.Exception is not null)
        {
            if (message.Exception.GetType() == typeof(GatewayReconnectException))
            {
                logger.Log(
                    LogLevel.Trace,
                    message.Exception,
                    "{Source} | {Message}",
                    message.Source,
                    message.Exception.Message
                );
            }
            else
            {
                logger.Log(
                    level,
                    message.Exception,
                    "{Source} | {Message}",
                    message.Source,
                    message.Message ?? message.Exception.Message
                );
            }
        }
        else
        {
            logger.Log(level, "{Source} | {Message}", message.Source, message.Message);
        }
        return Task.CompletedTask;
    }

    private async Task Client_Ready()
    {
        logger.LogInformation(
            "Logged in as {user}#{discriminator} ({id})",
            client.CurrentUser?.Username,
            client.CurrentUser?.Discriminator,
            client.CurrentUser?.Id
        );

        await commandHandler.OnReady(Assembly.GetExecutingAssembly());

        customStatusService.StartBackgroundTask(cts.Token);
    }
}
