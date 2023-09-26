﻿using DibariBot.Database;
using Microsoft.EntityFrameworkCore;

namespace DibariBot.Modules.ConfigCommand.Pages;

public class DefaultMangaPage : ConfigPage
{
    public class DefaultMangaSetModal : IModal
    {
        public string Title => "Set Default Manga - Step 1";

        [ModalTextInput(customId: ModulePrefixes.CONFIG_DEFAULT_MANGA_SET_MANGA_INPUT)]
        public string URL { get; set; } = "";
    }

    public struct ConfirmState
    {
        public SeriesIdentifier series;
        /// <summary>
        /// channel ID. 0ul means we want the default to be the server instead.
        /// </summary>
        public ulong channelId;

        public ConfirmState()
        {
        }

        public ConfirmState(SeriesIdentifier series, ulong channelId)
        {
            this.series = series;
            this.channelId = channelId;
        }
    }

    public override Page Id => Page.DefaultManga;

    public override string Label => "Default manga";

    public override string Description => "Change the manga that opens when no URL is specified. Can be per-server and per-channel.";

    private readonly DbService dbService;
    private readonly ConfigCommandService configCommandService;
    private readonly BotConfig config;

    public DefaultMangaPage(DbService db, ConfigCommandService configCommandService, BotConfig config)
    {
        dbService = db;
        this.configCommandService = configCommandService;
        this.config = config;
    }

    // step 1 - help page/modal open
    public override async Task<MessageContents> GetMessageContents(ConfigCommandService.State state)
    {
        var embed = GetCurrentDefaultsEmbed(await GetMangaDefaultsList());

        var components = new ComponentBuilder()
            .WithSelectMenu(ConfigPageUtility.GetPageSelectDropdown(configCommandService.ConfigPages, Id))
            .WithButton(new ButtonBuilder()
                .WithLabel("Set")
                .WithCustomId($"{ModulePrefixes.CONFIG_DEFAULT_MANGA_SET}")
                .WithStyle(config.PrimaryButtonStyle))
            .WithButton(new ButtonBuilder()
                .WithLabel("Remove")
                .WithCustomId($"{ModulePrefixes.CONFIG_DEFAULT_MANGA_REMOVE}")
                .WithStyle(config.PrimaryButtonStyle))
            .WithRedButton();

        return new MessageContents("", embed.Build(), components);
    }

    private async Task<DibariBot.Core.Database.Models.DefaultManga[]> GetMangaDefaultsList()
    {
        using var dbContext = dbService.GetDbContext();

        var guildId = (Context.Guild?.Id) ?? 0ul;

        DibariBot.Core.Database.Models.DefaultManga[] defaults;
        if (guildId == 0ul)
        {
            defaults = await dbContext.DefaultMangas.Where(x => x.ChannelId == Context.Channel.Id).ToArrayAsync();
        }
        else
        {
            defaults = await dbContext.DefaultMangas.Where(x => x.GuildId == guildId).ToArrayAsync();
        }

        return defaults;
    }

    private static EmbedBuilder GetCurrentDefaultsEmbed(DibariBot.Core.Database.Models.DefaultManga[] defaults)
    {
        var embed = new EmbedBuilder();

        if (defaults.Length > 0)
        {
            foreach (var def in defaults)
            {
                embed.AddField(def.ChannelId == 0ul ? "Server-wide" : $"<#{def.ChannelId}>", def.Manga);
            }
        }
        else
        {
            embed.WithDescription("No defaults set.");
        }

        return embed;
    }

    [ComponentInteraction(ModulePrefixes.CONFIG_DEFAULT_MANGA_REMOVE_DROPDOWN)]
    public async Task RemoveMangaDropdown(string id)
    {
        await DeferAsync();

        ulong channelId = ulong.Parse(id);

        using var context = dbService.GetDbContext();

        var guildId = Context.Guild?.Id ?? 0ul;

        var farts = await context.DefaultMangas.FirstOrDefaultAsync(x =>
            x.GuildId == guildId && x.ChannelId == channelId
        );

        if (farts == null)
        {
            await ModifyOriginalResponseAsync(new MessageContents("Couldn't find it anymore?", embed: null, null));
            return;
        }

        context.Remove(farts);

        await context.SaveChangesAsync();

        await ModifyOriginalResponseAsync(await GetMessageContents(new ConfigCommandService.State()
        {
            page = Page.DefaultManga
        }));
    }

    [ComponentInteraction(ModulePrefixes.CONFIG_DEFAULT_MANGA_REMOVE)]
    public async Task RemoveMangaButton()
    {
        await DeferAsync();

        var defaults = await GetMangaDefaultsList();

        var embed = GetCurrentDefaultsEmbed(defaults);
        var cancelButton = new ButtonBuilder()
                .WithLabel("Cancel")
                .WithStyle(config.PrimaryButtonStyle)
                .WithCustomId(ModulePrefixes.CONFIG_PAGE_SELECT_PAGE_BUTTON +
                    StateSerializer.SerializeObject(StateSerializer.SerializeObject(Id))
                );

        if (defaults.Length <= 0)
        {
            await ModifyOriginalResponseAsync(new MessageContents(string.Empty, embed.Build(),
                new ComponentBuilder().WithButton(cancelButton)));
            return;
        }

        var options = new List<SelectMenuOptionBuilder>();

        foreach (var def in defaults)
        {
            var channelName = def.ChannelId == 0 ?
                    "Server-wide" :
                    $"#{(await Context.Client.GetChannelAsync(def.ChannelId)).Name}";

            options.Add(new SelectMenuOptionBuilder()
                    .WithLabel(channelName)
                    .WithDescription(def.Manga)
                    .WithValue(def.ChannelId.ToString()));
        }

        var components = new ComponentBuilder()
            .WithSelectMenu(new SelectMenuBuilder()
                .WithOptions(options)
                .WithCustomId(ModulePrefixes.CONFIG_DEFAULT_MANGA_REMOVE_DROPDOWN)
                )
            .WithButton(cancelButton);

        await ModifyOriginalResponseAsync(new MessageContents(string.Empty, embed.Build(), components));
    }

    [ComponentInteraction(ModulePrefixes.CONFIG_DEFAULT_MANGA_SET)]
    public async Task OpenModal()
    {
        await RespondWithModalAsync<DefaultMangaSetModal>(ModulePrefixes.CONFIG_DEFAULT_MANGA_SET_MODAL);
    }

    // step 2 - confirm section
    [ModalInteraction($"{ModulePrefixes.CONFIG_DEFAULT_MANGA_SET_MODAL}")]
    public async Task OnModalResponse(DefaultMangaSetModal modal)
    {
        await DeferAsync();

        var parsedUrl = ParseUrl.ParseMangaUrl(modal.URL);

        if (parsedUrl == null || parsedUrl.Value.platform == null && parsedUrl.Value.series == null)
        {
            var errorEmbed = new EmbedBuilder()
                .WithDescription("Unsupported/invalid URL. Please make sure you're using a link that is supported by the bot."); // TODO: l18n

            await ModifyOriginalResponseAsync(new MessageContents(string.Empty, errorEmbed.Build(), null));
            return;
        }

        var channelId = 0ul;
        if (Context.Guild == null)
        {
            channelId = Context.Channel.Id;
        }

        await ModifyOriginalResponseAsync(ConfirmPromptContents(new ConfirmState(parsedUrl.Value, channelId)));
        return;
    }

    [ComponentInteraction(ModulePrefixes.CONFIG_DEFAULT_MANGA_SET_CHANNEL_INPUT + "*")]
    public async Task OnChannelSet(string id, IChannel[] channel)
    {
        // should be doing UpdateAsync but i have no clue how to get that kekw
        await DeferAsync();
        await ModifyOriginalResponseAsync(
            ConfirmPromptContents(
                new ConfirmState(StateSerializer.DeserializeObject<SeriesIdentifier>(id),
                channel.Length > 0 ? channel[0].Id : 0ul)));
    }

    private MessageContents ConfirmPromptContents(ConfirmState confirmState)
    {
        var embed = new EmbedBuilder()
            .WithDescription($"Set the default manga for **{(confirmState.channelId == 0ul ? "the server" : $"<#{confirmState.channelId}>")}** as **{confirmState.series}**?");

        var components = new ComponentBuilder();

        if (Context.Guild != null)
        {
            components.WithSelectMenu(new SelectMenuBuilder()
            .WithCustomId(ModulePrefixes.CONFIG_DEFAULT_MANGA_SET_CHANNEL_INPUT + StateSerializer.SerializeObject(confirmState.series))
            .WithType(ComponentType.ChannelSelect)
            .WithPlaceholder("(Optional) channel.")
            .WithMinValues(0)
            .WithMaxValues(1)
            .WithChannelTypes(
                ChannelType.Text, ChannelType.News, ChannelType.NewsThread,
                ChannelType.PublicThread, ChannelType.PrivateThread, ChannelType.Forum));
        }

        components.WithButton(new ButtonBuilder()
            .WithLabel("Yes!")
            .WithCustomId(ModulePrefixes.CONFIG_DEFAULT_MANGA_SET_SUBMIT_BUTTON + StateSerializer.SerializeObject(confirmState))
            .WithStyle(config.PrimaryButtonStyle));

        components.WithButton(new ButtonBuilder()
            .WithLabel("Cancel")
            .WithCustomId(ModulePrefixes.CONFIG_PAGE_SELECT_PAGE_BUTTON +
                StateSerializer.SerializeObject(StateSerializer.SerializeObject(Id)))
            .WithStyle(ButtonStyle.Danger));

        return new MessageContents(string.Empty, embed.Build(), components);
    }

    // step 3 - we've got a submit!!
    [ComponentInteraction(ModulePrefixes.CONFIG_DEFAULT_MANGA_SET_SUBMIT_BUTTON + "*")]
    public async Task OnConfirmed(string id)
    {
        await DeferAsync();

        var state = StateSerializer.DeserializeObject<ConfirmState>(id);

        var toAdd = new DibariBot.Core.Database.Models.DefaultManga()
        {
            GuildId = Context.Guild?.Id ?? 0ul,
            ChannelId = state.channelId,
            Manga = state.series.ToString()
        };

        // Why is this not a thing yet: https://github.com/dotnet/efcore/issues/4526
        using (var context = dbService.GetDbContext())
        {
            var exists = await context.DefaultMangas.FirstOrDefaultAsync(x => x.GuildId == toAdd.GuildId && x.ChannelId == toAdd.ChannelId);

            if (exists != null)
            {
                exists.Manga = toAdd.Manga;
            }
            else
            {
                context.DefaultMangas.Add(toAdd);
            }

            await context.SaveChangesAsync();
        }

        await ModifyOriginalResponseAsync(
            await configCommandService.GetMessageContents(new ConfigCommandService.State()
            {
                page = Id
            }, Context));
    }
}
