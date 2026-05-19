using Discord.Commands;

namespace DibariBot.Modules.About;

public class AboutPrefixModule(AboutService aboutService) : PrefixModule
{
    [Command("about")]
    [ParentModulePrefix(typeof(AboutModule))]
    public async Task AboutCommand()
    {
        if (Context.Guild is { Id: 695200821910044783ul })
            return;

        var component = await aboutService.BuildComponent();

        await ReplyAsync(components: component, allowedMentions: AllowedMentions.None);
    }
}