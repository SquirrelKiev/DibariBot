using Serilog;
using Serilog.Events;
using YamlDotNet.Serialization;

namespace DibariBot
{
    public class BotConfig
    {
        [YamlMember(Description = "DON'T CHANGE!")]
        public int Version { get; set; } = 1;

        [YamlMember(Description = @"Your bot token from https://discord.com/developers/applications. Don't share!")]
        public string BotToken { get; set; } = "BOT_TOKEN_HERE";

        [YamlMember(Description = "The type of cache to use.\n" +
                                  "Options are currently only \"Memory\".")]
        public CacheType Cache { get; set; } = CacheType.Memory;

        [YamlMember(Description = "The type of database to use.\n" +
                                  "Options are \"Sqlite\" and \"Postgresql\".")]
        public DatabaseType Database { get; set; } = DatabaseType.Sqlite;

        [YamlMember(Description = "The connection string for the database specified above.\n" +
                                "Example Postgres string: Host=127.0.0.1;Username=postgres;Password=;Database=botdb\n" +
                                "Example Sqlite string: Data Source=data/BotDb.db")]
        public string DatabaseConnectionString { get; set; } = "Data Source=data/DibariBot.db";

        [YamlMember(Description = "The reaction to put on prefix commands when an unhandled error occurs. Will only appear on prefix commands.")]
        public string ErrorEmote { get; set; } = "\u2753";

        [YamlMember(Description = "A set of UserIDs. Users in this set will be granted permission to use commands to manage the instance itself.\n" +
                                  "This is a dangerous permission to grant.")]
        public HashSet<ulong> ManagerUserIds { get; set; } = [0ul];

        [YamlMember(Description = "An optional URL to an instance of Seq. Empty string is interpreted as not wanting Seq.")]
        public string SeqUrl { get; set; } = "";

        [YamlMember(Description = "An optional API key for Seq. Empty string is interpreted as no API key.")]
        public string SeqApiKey { get; set; } = "";

        [YamlMember(Description = "The logging level to use.")]
        public LogEventLevel LogEventLevel { get; set; } = LogEventLevel.Information;

        [YamlMember(Description = "The base URL for Cubari requests.\n" +
                                  "Only really should be changed if you're using a self-hosted instance for whatever reason.")]
        public string CubariUrl { get; set; } = "https://cubari.moe";

        [YamlMember(Description = "The URL for MangaDex searches.")]
        public string MangaDexApiUrl { get; set; } = "https://api.mangadex.org";

        [YamlMember(Description = "The URL to link to for MangaDex searches. '{{ID}}' will be replaced with the mangadex id.")]
        public string MangaDexSearchUrl { get; set; } = "https://mangadex.org/title/{{ID}}";

        [YamlMember(Description = "The URL for Phixiv requests. expects an instance of https://github.com/HazelTheWitch/phixiv")]
        public string PhixivUrl { get; set; } = "https://www.phixiv.net";

        [YamlMember(Description = "How many results to show per page when using the search command.")]
        public int MangaDexSearchLimit { get; set; } = 5;

        // why is this a config value?
        [YamlMember(Description = "How long a manga title should be before it is truncated.")]
        public int MaxTitleLength { get; set; } = 50;

        [YamlMember(Description = "How long a manga description should be before it is truncated.")]
        public int MaxDescriptionLength { get; set; } = 200;

        [YamlMember(Description = "The timeout for user specified regex. Applies to all regex as a whole, not per regex.\n" +
                                "e.g. if there were 5 filters, each filter's time running would be deducted from the total timeout for that filter check.\n" +
                                "(hope that makes sense)")]
        public uint RegexTimeoutMilliseconds { get; set; } = 250;

        [YamlIgnore]
        public TimeSpan RegexTimeout => TimeSpan.FromMilliseconds(RegexTimeoutMilliseconds);

        [YamlMember(Description = "***** IMAGE PROXY CONFIG *****\n\n" +
                                "Specifies how the URL to proxy should be encoded.")]
        public ProxyUrlEncodingFormat ProxyUrlEncoding { get; set; } = ProxyUrlEncodingFormat.Base64;

        [YamlMember(Description = "The URL pattern to use for proxying images (will only be used if the platform requires it.\n" +
            "{{URL}} will be replaced with the url in the encoding specified above.")]
        public string ProxyUrl { get; set; } = "https://services.f-ck.me/v1/image/{{URL}}?source=dibari_bot";

        [YamlMember(Description = "What platforms should have their images proxied.")]
        public string[] PlatformsToProxy { get; set; } = ["mangadex"];

        [YamlMember(Description = "***** ABOUT PAGE *****\n" +
                                  "For any string here, the following will be replaced:\n" +
                                  "- {{guilds}} will be substituted with how many guilds (servers) the bot is in.\n" +
                                  "- {{botUsername}} will be substituted with the bot's username.\n" +
                                  "\n" +
                                  "The about page title.")]
        public string AboutPageTitle { get; set; } = "About {{botUsername}}";

        [YamlMember(Description = "The about page description.")]
        public string AboutPageDescription { get; set; } = "A discord bot for reading manga, within Discord.";

        [YamlMember(Description = "Fields within the about page.")]
        public AboutField[] AboutPageFields { get; set; } =
        [
            new AboutField
            {
                Name = "Total Servers:",
                Value = "{{guilds}}"
            },
            new AboutField
            {
                Name = "Credits:",
                Value = "Bot by [SquirrelKiev](https://github.com/SquirrelKiev)"
            },
            new AboutField
            {
                Name = "Source Code:",
                Value = "https://github.com/SquirrelKiev/DibariBot"
            }
        ];

        [YamlMember(Description = "The color to use for all embeds (if not set by the Guild).\n" +
                                  "Ideally this should be formatted as a hex number. Default for example is 0x5E69A3")]
        public int DefaultEmbedColor { get; set; } = 0x5E69A3;

        [YamlMember(Description = "The color to use for all error embeds. \n" +
                                  "Ideally this should be formatted as a hex number. Default for example is 0xE74C3C")]
        public int ErrorEmbedColor { get; set; } = 0xE74C3C;

        public struct AboutField
        {
            public string Name { get; set; }
            public string Value { get; set; }
            public bool Inline { get; set; }
        }

        public enum CacheType
        {
            Memory
        }

        public enum DatabaseType
        {
            Sqlite,
            Postgresql
        }

        public enum ProxyUrlEncodingFormat
        {
            UrlEscaped,
            Base64
        }

        public virtual bool IsValid()
        {
            try
            {
                TokenUtils.ValidateToken(TokenType.Bot, BotToken);
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Supplied bot token is invalid.");
                return false;
            }

            return true;
        }
    }
}
