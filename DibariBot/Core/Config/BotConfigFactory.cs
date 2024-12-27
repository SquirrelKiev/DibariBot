using System.Diagnostics.CodeAnalysis;
using Serilog;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace DibariBot;

public class BotConfigFactory
{
    public static readonly string DefaultDataDirectory = Path.Combine(Path.Combine(AppContext.BaseDirectory, "data"));
    private static readonly string ConfigPath = Environment.GetEnvironmentVariable("BOT_CONFIG_LOCATION") ??
                                                Path.Combine(DefaultDataDirectory, "bot_config.yaml");

    public bool GetConfig([NotNullWhen(true)] out BotConfig? botConfig)
    {
        var serializer = new SerializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .WithDefaultScalarStyle(ScalarStyle.DoubleQuoted)
            .Build();

        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        if (!File.Exists(ConfigPath))
        {
            botConfig = new BotConfig();

            Log.Fatal("Config not found. Creating new config at {ConfigPath}. Please edit this file and restart the bot.", ConfigPath);

            var dirName = Path.GetDirectoryName(ConfigPath);
            if (dirName != null)
                Directory.CreateDirectory(dirName);

            File.WriteAllText(ConfigPath, serializer.Serialize(botConfig));
            return false;
        }

        try
        {
            botConfig = deserializer.Deserialize<BotConfig>(File.ReadAllText(ConfigPath));
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Failed to read config.");
            botConfig = new BotConfig();
            return false;
        }

        //#if DEBUG
        //botConfig.GenerateMetadata();
        File.WriteAllText(ConfigPath, serializer.Serialize(botConfig));
        //#endif

        return true;

    }
}
