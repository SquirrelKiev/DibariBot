using System.Reflection;
using DibariBot.Database;
using DibariBot.Database.Models;
using DibariBot.Modules;
using DibariBot.Modules.Manga;
using Discord.Commands;
using Discord.Interactions;
using Discord.Rest;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

// ReSharper disable ConditionalAccessQualifierIsNonNullableAccordingToAPIContract

namespace DibariBot;

public class CommandHandler(
    InteractionService interactionService,
    CommandService commandService,
    DiscordSocketClient client,
    BotConfig botConfig,
    IServiceProvider services,
    DbService dbService,
    ColorProvider colorProvider,
    MangaFactory mangaFactory,
    ILogger<CommandHandler> logger
)
{
    private bool runOnce = false;
    
    public async Task OnReady(params Assembly[] assemblies)
    {
        if (runOnce)
            return;
        
        try
        {
            await using var context = dbService.GetDbContext();
            await InitializeInteractionService(assemblies, context);
            await InitializeCommandService(assemblies, context);

            runOnce = true;
            //await RegisterAllGuildSlashes(context);
        }
        catch (Exception e)
        {
            logger.LogCritical(e, "Failed to register commands/interactions!");
        }
    }

    #region Prefix Command Handling

    protected async Task MessageReceived(SocketMessage msg)
    {
        if (msg.Author.IsBot)
            return;

        if (msg is not SocketUserMessage userMessage)
            return;

        try
        {
            await RunCommand(userMessage);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Command failed: ");
        }
    }

    protected async Task RunCommand(SocketUserMessage userMessage)
    {
        // TODO: Merge the dbContext calls here
        var prefix = await GetPrefix(userMessage.Channel);

        var commandNameStart = 0;
        if (!userMessage.HasStringPrefix(prefix, ref commandNameStart))
        {
            return;
        }

        var commandContext = new SocketCommandContext(client, userMessage);

        var aliasAttempt = await ExecutePrefixAlias(userMessage, commandNameStart, commandContext);

        if (!aliasAttempt)
            await commandService.ExecuteAsync(commandContext, commandNameStart, services);
    }

    // terrible hacks
    private async Task<bool> ExecutePrefixAlias(
        SocketUserMessage userMessage,
        int commandNameStart,
        SocketCommandContext commandContext
    )
    {
        var commandName = GetCommandName(userMessage, commandNameStart);

        await using var context = dbService.GetDbContext();

        var alias = await context.MangaCommandAliases.AnyAsync(x =>
            x.GuildId == commandContext.Guild.Id && x.SlashCommandName == commandName
        );

        if (!alias)
            return false;

        var mangaCommands = commandService
            .Commands.Where(x => x.Name == "manga")
            .OrderByDescending(x => x.Priority);

        int i = 0;
        foreach (var mangaCommand in mangaCommands)
        {
            var parseResult = await mangaCommand.ParseAsync(
                commandContext,
                commandNameStart + commandName.Length,
                SearchResult.FromSuccess(userMessage.Content, null)
            );

            if (!parseResult.IsSuccess)
            {
                logger.LogTrace(
                    "failed parse for {command} {commandArgs} ({index}) {reason}",
                    commandName,
                    mangaCommand.Parameters.Select(x => x.Name),
                    i,
                    parseResult.ErrorReason
                );
                i++;
                continue;
            }

            logger.LogTrace(
                "parse successful for {command} {commandArgs} ({index})",
                commandName,
                mangaCommand.Parameters.Select(x => x.Name),
                i
            );

            await mangaCommand.ExecuteAsync(commandContext, parseResult, services);
            return true;
        }

        return false;
    }

    public static string GetCommandName(SocketUserMessage userMessage, int commandNameStart)
    {
        var commandNameEnd = userMessage.Content[commandNameStart..].IndexOf(' ');

        var commandName =
            commandNameEnd == -1
                ? userMessage.Content[commandNameStart..]
                : userMessage.Content.Substring(commandNameStart, commandNameEnd);
        return commandName;
    }

    public async Task<string> GetPrefix(IChannel? channel)
    {
        var prefix = GuildConfig.DefaultPrefix;

        if (channel is SocketTextChannel textChannel)
        {
            await using var context = dbService.GetDbContext();
            var config = await context.GetGuildConfig(textChannel.Guild.Id);

            prefix = config.Prefix;
        }

        return prefix;
    }

    protected async Task CommandExecuted(
        Optional<CommandInfo> cmdInfoOpt,
        ICommandContext ctx,
        Discord.Commands.IResult res
    )
    {
        if (res.IsSuccess)
            return;

        if (res.Error != CommandError.Exception && res.Error != CommandError.UnmetPrecondition)
            return;

        try
        {
            if (res is Discord.Commands.PreconditionResult precondResult)
            {
                var messageBody =
                    $"Condition to use the command not met. {precondResult.ErrorReason}";
                await ctx.Message.ReplyAsync(messageBody);
            }
            else
            {
                IEmote emote;

                if (Emote.TryParse(botConfig.ErrorEmote, out var result))
                {
                    emote = result;
                }
                else
                {
                    emote = Emoji.Parse(botConfig.ErrorEmote);
                }

                await ctx.Message.AddReactionAsync(emote);
            }
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "Failed to add the error reaction!");
        }
    }

    #endregion

    #region Interaction Handling

    protected async Task InteractionExecuted(
        ICommandInfo cmdInfo,
        IInteractionContext ctx,
        Discord.Interactions.IResult res
    )
    {
        if (res.IsSuccess || res.Error == InteractionCommandError.UnknownCommand)
            return;

        var messageBody = $"{res.Error}, {res.ErrorReason}";

        if (res is Discord.Interactions.PreconditionResult precondResult)
        {
            messageBody = $"Condition to use this command not met. {precondResult.ErrorReason}";
        }

        if (ctx.Interaction.HasResponded)
        {
            await ctx.Interaction.ModifyOriginalResponseAsync(
                new MessageContents(messageBody, embed: null, null)
            );
        }
        else
        {
            await ctx.Interaction.RespondAsync(messageBody, ephemeral: true);
        }
    }

    protected async Task InteractionCreated(SocketInteraction arg)
    {
        var ctx = new SocketInteractionContext(client, arg);

        if (ctx.Interaction is SocketMessageComponent componentInteraction)
        {
            var ogRes = componentInteraction.Message;

            var ogAuthor = ogRes.Interaction?.User.Id;

            // horrible
            if (ogAuthor == null)
            {
                var channel = (ISocketMessageChannel)
                    await client.GetChannelAsync(ogRes.Reference.ChannelId);
                var message = await channel.GetMessageAsync(ogRes.Reference.MessageId.Value);
                ogAuthor = message?.Author?.Id;
            }

            if (ogAuthor != null && ogAuthor != ctx.Interaction.User.Id)
            {
                await componentInteraction.RespondAsync(
                    "You did not originally trigger this. Please run the command yourself.",
                    ephemeral: true
                );

                return;
            }
        }

        var res = await interactionService.ExecuteCommandAsync(ctx, services);

        if (res.Error == InteractionCommandError.UnknownCommand && ctx.Guild != null)
        {
            var commandData = ((SocketSlashCommand)ctx.Interaction).Data;

            await using var context = dbService.GetDbContext();

            var executeRes = await TryExecuteAliasSlash(ctx, commandData, context);

            if (executeRes == false)
            {
                var eb = new EmbedBuilder()
                    .WithDescription("Couldn't find that command. This is most definitely a bug!")
                    .WithColor(colorProvider.GetErrorEmbedColor());

                // TODO: Remove this ping once stable
                await ctx.Interaction.RespondAsync("<@298037013964259329>", embed: eb.Build());
            }
        }
    }

    // Manga alias command hacks cuz discord.net doesn't allow outsiders to set values on params
    private async Task<bool> TryExecuteAliasSlash(
        SocketInteractionContext ctx,
        SocketSlashCommandData commandData,
        BotDbContext context
    )
    {
        var alias = await context.MangaCommandAliases.FirstOrDefaultAsync(x =>
            x.GuildId == ctx.Guild.Id && x.SlashCommandName == commandData.Name
        );

        if (alias == null)
        {
            return false;
        }

        var assembly = Assembly.GetAssembly(typeof(RestGuild))!;
        var applicationCommandOptionType = assembly.GetType(
            "Discord.API.ApplicationCommandInteractionDataOption"
        )!;
        var applicationCommandOptionInstance = Activator.CreateInstance(
            applicationCommandOptionType
        )!;

        applicationCommandOptionType
            .GetProperty("Name")
            ?.SetValue(applicationCommandOptionInstance, "url");
        applicationCommandOptionType
            .GetProperty("Value")
            ?.SetValue(applicationCommandOptionInstance, new Optional<object>(alias.Manga));
        applicationCommandOptionType
            .GetProperty("Type")
            ?.SetValue(applicationCommandOptionInstance, ApplicationCommandOptionType.String);

        var socketSlashCommandDataOptionType = typeof(SocketSlashCommandDataOption);
        var socketSlashCommandDataOptionInstance = Activator.CreateInstance(
            socketSlashCommandDataOptionType,
            BindingFlags.Instance | BindingFlags.NonPublic,
            null,
            [commandData, applicationCommandOptionInstance],
            null
        )!;

        var optionsProperty = commandData.GetType().GetProperty(nameof(commandData.Options))!;
        var newOptions = commandData
            .Options.Where(x => x.Name != "url")
            .Append((SocketSlashCommandDataOption)socketSlashCommandDataOptionInstance)
            .ToArray();

        optionsProperty.SetValue(commandData, newOptions);

        var mangaCommand = interactionService.SlashCommands.First(x => x.Name == "manga");
        await mangaCommand.ExecuteAsync(ctx, services);

        return true;
    }

    #endregion

    protected async Task InitializeInteractionService(Assembly[] assemblies, BotDbContext context)
    {
        foreach (var assembly in assemblies)
        {
            var modules = await interactionService.AddModulesAsync(assembly, services);

            foreach (var moduleInfo in modules)
            {
                logger.LogTrace("Registered Interaction Module: {moduleName}", moduleInfo.Name);
            }
        }

        await interactionService.RegisterCommandsGloballyAsync();

        // Do I need to call this on start-up?
        //await RefreshAllGuildSlashes(context);

        client.InteractionCreated += InteractionCreated;
        interactionService.InteractionExecuted += InteractionExecuted;
    }

    public async Task RegisterAllGuildSlashes(BotDbContext context)
    {
        var commandAliases = (await context.MangaCommandAliases.ToArrayAsync())
            .GroupBy(x => client.GetGuild(x.GuildId))
            .Where(x => x.Key != null);

        var missingGuilds = client
            .Guilds.Where(x => commandAliases.All(y => y.Key.Id != x.Id))
            .ToArray();

        var mangaCommand = GetMangaCommand();
        foreach (var alias in commandAliases)
        {
            try
            {
                await RegisterGuildSlashes(alias.Key, alias.ToArray(), mangaCommand);
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Failed to register Guild slashes for {guild}.",
                    alias?.Key?.Id
                );
            }
        }

#pragma warning disable IDE0301 // Simplify collection initialization - has different semantics. Compiles down to Array.Empty instead of Enumerable.Empty
        foreach (var guild in missingGuilds)
        {
            try
            {
                await guild.DeleteApplicationCommandsAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to delete Guild slashes for {guild}.", guild?.Id);
            }
        }
#pragma warning restore IDE0301
    }

    public async Task RegisterGuildSlashes(SocketGuild guild, BotDbContext context)
    {
        var aliases = await context
            .MangaCommandAliases.Where(x => x.GuildId == guild.Id)
            .ToArrayAsync();

        if (aliases.Length == 0)
        {
            await guild.DeleteApplicationCommandsAsync();
        }
        else
        {
            await RegisterGuildSlashes(guild, aliases, GetMangaCommand());
        }
    }

    public async Task RegisterGuildSlashes(
        SocketGuild guild,
        MangaCommandAlias[] aliases,
        SlashCommandInfo mangaCommand
    )
    {
        var list = new List<ApplicationCommandProperties>(aliases.Length);
        foreach (var x in aliases)
        {
            var seriesId = ParseUrl.ParseMangaUrl(x.Manga);

            if (seriesId == null)
            {
                continue;
            }

            var manga = await mangaFactory.GetManga(seriesId.Value);

            if(manga == null)
                continue;

            var mangaMetadata = await manga.GetMetadata();

            list.Add(
                ConvertSlash(mangaCommand)
                    .WithName(x.SlashCommandName)
                    .WithDescription($"Gets a page from a chapter of {mangaMetadata.title.Truncate(botConfig.MaxTitleLength)}")
                    .Build()
            );
        }

        var commands = list.ToArray<ApplicationCommandProperties>();

        await guild.BulkOverwriteApplicationCommandAsync(commands);
    }

    private SlashCommandInfo GetMangaCommand() =>
        interactionService.SlashCommands.First(x => x.Name == "manga");

    private SlashCommandBuilder ConvertSlash(SlashCommandInfo command)
    {
        var commandBuilder = new SlashCommandBuilder()
            .WithName(command.Name)
            .WithDescription(command.Description)
            .WithDefaultPermission(command.DefaultPermission)
            .WithNsfw(command.IsNsfw)
            .WithDefaultMemberPermissions(command.DefaultMemberPermissions)
            .WithIntegrationTypes(command.IntegrationTypes.ToArray())
            .WithContextTypes(command.ContextTypes.ToArray());

        commandBuilder.AddOptions(
            command
                .FlattenedParameters.Where(x => x.Name != "url")
                .Select(ConvertSlashParameter)
                .ToArray()
        );

        return commandBuilder;
    }

    private SlashCommandOptionBuilder ConvertSlashParameter(SlashCommandParameterInfo paramInfo)
    {
        var optionBuilder = new SlashCommandOptionBuilder()
            .WithName(paramInfo.Name)
            .WithDescription(paramInfo.Description)
            .WithRequired(paramInfo.IsRequired)
            .WithAutocomplete(paramInfo.IsAutocomplete);

        if (paramInfo.MinValue.HasValue)
        {
            optionBuilder.WithMinValue(paramInfo.MinValue.Value);
        }

        if (paramInfo.MaxValue.HasValue)
        {
            optionBuilder.WithMaxValue(paramInfo.MaxValue.Value);
        }

        if (paramInfo.MinLength.HasValue)
        {
            optionBuilder.WithMinLength(paramInfo.MinLength.Value);
        }

        if (paramInfo.MaxLength.HasValue)
        {
            optionBuilder.WithMaxLength(paramInfo.MaxLength.Value);
        }

        if (paramInfo.DiscordOptionType.HasValue)
        {
            optionBuilder.WithType(paramInfo.DiscordOptionType.Value);
        }

        foreach (var choice in paramInfo.Choices)
        {
            if (choice.Value is string stringValue)
            {
                optionBuilder.AddChoice(choice.Name, stringValue);
            }
            else if (choice.Value is double doubleValue)
            {
                optionBuilder.AddChoice(choice.Name, doubleValue);
            }
            else if (choice.Value is float floatValue)
            {
                optionBuilder.AddChoice(choice.Name, floatValue);
            }
            else if (choice.Value is int intValue)
            {
                optionBuilder.AddChoice(choice.Name, intValue);
            }
            else if (choice.Value is long longValue)
            {
                optionBuilder.AddChoice(choice.Name, longValue);
            }
        }

        foreach (var channelType in paramInfo.ChannelTypes)
        {
            optionBuilder.AddChannelType(channelType);
        }

        foreach (var complexParameterField in paramInfo.ComplexParameterFields)
        {
            var complexOptionBuilder = ConvertSlashParameter(complexParameterField);
            optionBuilder.AddOption(complexOptionBuilder);
        }

        return optionBuilder;
    }

    protected async Task InitializeCommandService(Assembly[] assemblies, BotDbContext context)
    {
        foreach (var assembly in assemblies)
        {
            var modules = await commandService.AddModulesAsync(assembly, services);

            foreach (var moduleInfo in modules)
            {
                logger.LogTrace("Registered Prefix Module: {moduleName}", moduleInfo.Name);
            }
        }

        client.MessageReceived += MessageReceived;
        commandService.CommandExecuted += CommandExecuted;
    }
}
