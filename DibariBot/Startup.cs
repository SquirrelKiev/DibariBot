using System.Globalization;
using Microsoft.Extensions.Hosting;
using System.Text;
using Microsoft.Extensions.Logging;
using Serilog.Extensions.Logging;
using Serilog.Sinks.SystemConsole.Themes;
using DibariBot.Modules.About;
using DibariBot.Database;
using DibariBot.Modules.ConfigCommand.Pages;
using DibariBot.Modules.ConfigCommand;
using Discord.Commands;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Core;

namespace DibariBot;

public static class Startup
{
    public static async Task Main()
    {
        Logger? logger = null;
        try
        {
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

            Console.OutputEncoding = Encoding.UTF8;
            Log.Logger = new LoggerConfiguration().WriteTo
                .Console(
                    outputTemplate: "[FALLBACK] [{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();

            if (!new BotConfigFactory().GetConfig(out var botConfig))
            {
                Environment.Exit(1);
            }

            if (!botConfig.IsValid())
            {
                Environment.Exit(1);
            }

            var builder = new HostBuilder();

            var logLevel = botConfig.LogEventLevel;

            var logConfig = new LoggerConfiguration()
                    .MinimumLevel.Is(logLevel)
                    .Enrich.FromLogContext()
                    .Enrich.WithThreadId()
                    .Enrich.WithThreadName()
                    .Enrich.WithThreadName()
                    .WriteTo.Console(theme: AnsiConsoleTheme.Literate)
                ;

            Directory.CreateDirectory(BotConfigFactory.DefaultDataDirectory);

            if (!string.IsNullOrWhiteSpace(botConfig.SeqUrl))
            {
                logConfig.WriteTo.Seq(botConfig.SeqUrl, apiKey: botConfig.SeqApiKey);
            }

            logger = logConfig.CreateLogger();
            builder.ConfigureLogging(logging =>
                    logging.AddSerilog(logger)
                        .AddFilter<SerilogLoggerProvider>("Microsoft.Extensions.Http.DefaultHttpClientFactory",
                            LogLevel.Warning)
                        .AddFilter<SerilogLoggerProvider>("Microsoft.EntityFrameworkCore.*", LogLevel.Warning))
                ;

            builder.ConfigureServices(x => x.AddBotServices(botConfig));
            builder.ConfigureHostConfiguration(
                configBuilder => configBuilder.AddEnvironmentVariables(prefix: "DOTNET_"));

            await builder.RunConsoleAsync();
        }
        finally
        {
            if (logger != null)
                await logger.DisposeAsync();
        }
    }

    private static IServiceCollection AddBotServices(this IServiceCollection serviceCollection, BotConfig botConfig)
    {
        serviceCollection
            .AddSingleton(botConfig)
            .AddCache(botConfig)
            .AddSingleton(botConfig)
            .AddSingleton(new DiscordSocketClient(new DiscordSocketConfig
            {
                GatewayIntents = GatewayIntents.Guilds |
                                 GatewayIntents.MessageContent |
                                 GatewayIntents.GuildMessages |
                                 GatewayIntents.DirectMessages,
                LogLevel = LogSeverity.Verbose
            }))
            .AddSingleton(x => new InteractionService(x.GetRequiredService<DiscordSocketClient>(),
                new InteractionServiceConfig()
                {
                    LogLevel = LogSeverity.Verbose,
                    DefaultRunMode = Discord.Interactions.RunMode.Async
                }))
            .AddSingleton(new CommandService(new CommandServiceConfig
            {
                LogLevel = LogSeverity.Verbose,
                DefaultRunMode = Discord.Commands.RunMode.Async
            }))
            .AddSingleton<DbService>()
            .AddSingleton<CommandHandler>()
            .AddSingleton<AboutService>()
            .AddSingleton<ConfigCommandService>();
        ;

        serviceCollection.AddHttpClient(Microsoft.Extensions.Options.Options.DefaultName)
            .ConfigureHttpClient(DefaultHttpClientConfig);

        serviceCollection.ConfigureHttpClientDefaults(x => x.RemoveAllLoggers());

        // Stupid but im not sure how to do this with one scan and pass the inject option's param to WithLifetime
        serviceCollection.Scan(scan => scan.FromAssemblyOf<BotService>()
            .AddClasses(classes => classes.WithAttribute<InjectAttribute>(x =>
                x.ServiceLifetime == ServiceLifetime.Singleton)
            )
            .AsSelf()
            .WithSingletonLifetime()
        );

        serviceCollection.Scan(scan => scan.FromAssemblyOf<BotService>()
            .AddClasses(classes => classes.WithAttribute<InjectAttribute>(x =>
                x.ServiceLifetime == ServiceLifetime.Transient)
            )
            .AsSelf()
            .WithTransientLifetime()
        );

        serviceCollection.AddHostedService<BotService>();

        return serviceCollection;

        static void DefaultHttpClientConfig(HttpClient client)
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Dibari/DiscordBot (https://github.com/SquirrelKiev/DibariBotNew)");
            client.Timeout = TimeSpan.FromSeconds(10);
        }
    }

}