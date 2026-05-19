using Discord.Interactions;

namespace DibariBot.Modules.About;

[CommandContextType(InteractionContextType.Guild, InteractionContextType.BotDm, InteractionContextType.PrivateChannel)]
[IntegrationType(ApplicationIntegrationType.GuildInstall, ApplicationIntegrationType.UserInstall)]
public class AboutModule(AboutService aboutService) : BotModule
{
    [SlashCommand("about", "Info about the bot.")]
    [HelpPageDescription("Pulls up info about the bot.")]
    public async Task AboutSlash()
    {
        var component = await aboutService.BuildComponent();

        await RespondAsync(components: component, allowedMentions: AllowedMentions.None);
    }
}