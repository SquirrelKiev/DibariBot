using DibariBot.Database;
using Discord.WebSocket;

namespace DibariBot.Modules.Help;

[Inject(ServiceLifetime.Singleton)]
public class LazyHelpService(ColorProvider colorProvider, BotConfig botConfig, DiscordSocketClient client)
{
    public MessageContents GetMessageContents(string prefix, GuildConfig? guild)
    {
        if (!botConfig.PrefixCommandsSupport)
            prefix = $"@{client.CurrentUser.Username}#{client.CurrentUser.Discriminator} ";

        var eb = new EmbedBuilder()
            .WithDescription("Noting that any prefix command parameter wrapped in square brackets is an optional named parameter." +
                             $"\nUsage is pretty much how the command shows, just without square brackets. e.g.`{prefix}manga ch:123`" +
                             $"\n\n" +
                             $"https://github.com/SquirrelKiev/DibariBotNew/wiki is also a valuable resource!")
            .WithFields(
            [
                new EmbedFieldBuilder()
                    .WithName("/manga-search")
                    .WithValue("Searches MangaDex for the query provided. (searches titles, sorted by relevance)" +
                               "\n**Prefix versions**" +
                               $"\n`{prefix}search query [spoiler:false]` - if spoiler is specified, query must be wrapped in quotes."),
                new EmbedFieldBuilder()
                    .WithName("/manga")
                    .WithValue("Gets a page from a chapter of a manga." +
                               "\n**Prefix versions**" +
                               $"\n`{prefix}manga url chapter page`" +
                               $"\n`{prefix}manga chapter page`" +
                               $"\n`{prefix}manga chapter`" +
                               $"\n`{prefix}manga [url:\"\"] [ch:\"\"] [pg:1] [spoiler:false]`"),
                new EmbedFieldBuilder()
                    .WithName("/help")
                    .WithValue($"Shows this message.\n**Prefix versions**\n`{prefix}help`"),
                new EmbedFieldBuilder()
                    .WithName("/config")
                    .WithValue("Pulls up various options for configuring the bot."),
                new EmbedFieldBuilder()
                    .WithName("/about")
                    .WithValue($"Pulls up into about the bot\n**Prefix versions**\n`{prefix}about`"),

                ])
            .WithColor(colorProvider.GetEmbedColor(guild));

        return new MessageContents(eb);
    }
}