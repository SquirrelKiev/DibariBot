using Discord.Interactions;

namespace DibariBot.Modules.Manga;

[CommandContextType(InteractionContextType.Guild, InteractionContextType.BotDm, InteractionContextType.PrivateChannel)]
[IntegrationType(ApplicationIntegrationType.GuildInstall, ApplicationIntegrationType.UserInstall)]
public class MangaModule(MangaService mangaHandler) : BotModule
{
    public class JumpModal : IModal
    {
        public string Title => "Jump to Chapter/Page";

        [ModalTextInput(ModulePrefixes.MANGA_MODAL_CHAPTER_TEXTBOX)]
        public string Chapter { get; set; } = "";

        [ModalTextInput(ModulePrefixes.MANGA_MODAL_PAGE_TEXTBOX)]
        public int Page { get; set; }
    }

    [SlashCommand("manga", "Gets a page from a chapter of a manga.")]
    public async Task MangaSlash(
        [Summary(description: "The URL of the manga.")] string url = "",
        [Summary(description: "The chapter of the manga.")] string chapter = "",
        [Summary(description: "The page number.")] int page = 1,
        [Summary(description: "Whether the response should be ephemeral (only you can see it).")] bool ephemeral = false,
        [Summary(description: "Whether to spoiler tag the response or not.")] bool spoiler = false)
    {
        await DeferAsync(ephemeral);

        var contents = await mangaHandler.MangaCommand(Context.Guild?.Id ?? 0ul, GetParentChannel()?.Id ?? 0ul,
            url, chapter, page, ephemeral, spoiler);

        await FollowupAsync(contents, ephemeral);
    }

    [ModalInteraction(ModulePrefixes.MANGA_MODAL + "*")]
    public async Task MangaJumpModalInteraction(string rawState, JumpModal modal)
    {
        var state = StateSerializer.DeserializeObject<MangaService.State>(rawState);

        state.bookmark = new Bookmark(modal.Chapter, modal.Page);
        state.action = MangaAction.Open;
        state.bookmark.page -= 1;

        await CommonComponentHandling(state);
    }

    [ComponentInteraction($"{ModulePrefixes.MANGA_BUTTON}*")]
    public async Task MangaComponentInteraction(string rawState)
    {
        var state = StateSerializer.DeserializeObject<MangaService.State>(rawState);

        if (state.action == MangaAction.Jump)
        {
            await Context.Interaction.RespondWithModalAsync<JumpModal>(StateSerializer.SerializeObject(state, ModulePrefixes.MANGA_MODAL), modifyModal:
                builder =>
                {
                    builder.UpdateTextInput(ModulePrefixes.MANGA_MODAL_CHAPTER_TEXTBOX, input =>
                    {
                        input.Value = state.bookmark.chapter;
                    });
                    builder.UpdateTextInput(ModulePrefixes.MANGA_MODAL_PAGE_TEXTBOX, input =>
                    {
                        input.Value = (state.bookmark.page + 1).ToString();
                    });
                });
            return;
        }

        await CommonComponentHandling(state);
    }

    private async Task CommonComponentHandling(MangaService.State state)
    {
        await DeferAsync();

        var ogRes = await GetOriginalResponseAsync();

        var shouldResend = state.action == MangaAction.SendNonEphemeral;
        var isEphemeral = (ogRes.Flags & MessageFlags.Ephemeral) != 0 && !shouldResend;

        var contents =
            await mangaHandler.GetMangaMessage(Context.Guild?.Id ?? 0ul, GetParentChannel()?.Id ?? 0ul, state, isEphemeral);

        // it's probably an error
        if (contents.components == null || contents.components.Components.Sum(component => component.Components.Count) <= 1)
        {
            await FollowupAsync(contents, true);
            return;
        }

        if (shouldResend)
        {
            await FollowupAsync(contents);
            await Context.Interaction.DeleteOriginalResponseAsync();
        }
        else
        {
            await ModifyOriginalResponseAsync(contents);
        }
    }
}
