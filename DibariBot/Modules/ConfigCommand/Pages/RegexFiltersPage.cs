using System.Text;
using System.Text.RegularExpressions;
using DibariBot.Database.Models;
using DibariBot.Modules.Manga;
using Discord.Interactions;
using Humanizer;

namespace DibariBot.Modules.ConfigCommand.Pages;

[ConfigPage(Id, "Regex filters", "Specify filters to limit what mangas can be pulled up.", Conditions.NotInDm)]
public partial class RegexFiltersPage(
    MangaService mangaService,
    ConfigCommandService configCommandService,
    ColorProvider colorProvider)
    : BotModule, IConfigPage
{
    public class SetRegexModal : IModal
    {
        public string Title => "Set Regex";

        [ModalTextInput(customId: ModulePrefixes.CONFIG_FILTERS_MODAL_TEMPLATE_TEXTBOX, maxLength: 200)]
        public string Template { get; set; } = "";

        [ModalTextInput(customId: ModulePrefixes.CONFIG_FILTERS_MODAL_FILTER_TEXTBOX, maxLength: 200)]
        public string Filter { get; set; } = "";
    }

    public const Page Id = Page.RegexFilters;

    private const string EMBED_NAME_TEMPLATE = "Template";
    private const string EMBED_NAME_FILTER = "Filter";
    private const string EMBED_NAME_CHANNELS = "Channels";
    private const string EMBED_NAME_FILTER_TYPE = "Filter Type";
    private const string EMBED_NAME_SCOPE = "Channel Scope";

    public async Task<MessageContents> GetMessageContents(ConfigCommandService.State state)
    {
        var embed = new EmbedBuilder().WithColor(await colorProvider.GetEmbedColor(Context.Guild));

        // TODO: This needs paginating
        var filters = await mangaService.GetFilters(Context.Guild.Id);

        if (filters.Length > 0)
        {
            for (var i = 0; i < filters.Length; i++)
            {
                var filter = filters[i];

                var value = new StringBuilder()

                    .AppendLine("**Template**")
                    .AppendLine("```")
                    .AppendLine(filter.Template)
                    .AppendLine("```")

                    .AppendLine("**Filter**")
                    .AppendLine("```")
                    .AppendLine(filter.Filter)
                    .AppendLine("```")

                    .AppendLine("**Filter Type**")
                    .AppendLine(filter.FilterType.ToString())

                    .AppendLine("**Channel Scope**")
                    .AppendLine(filter.ChannelFilterScope.ToString())
                    .AppendLine("**Channels**");

                foreach (var channelEntry in filter.RegexChannelEntries)
                {
                    value.AppendLine($"<#{channelEntry.ChannelId}>");
                }

                embed.AddField(new EmbedFieldBuilder()
                    .WithName("Filter " + (i + 1))
                    .WithValue(value.ToString())
                    .WithIsInline(true));
            }
        }
        else
        {
            embed.WithDescription("No filters!");
        }

        var components = new ComponentBuilder()
            .WithSelectMenu(configCommandService.GetPageSelectDropdown(Id, IsUserInstallInteraction()))
            .WithButton(new ButtonBuilder()
                .WithLabel("Add")
                .WithCustomId($"{ModulePrefixes.CONFIG_FILTERS_OPEN_MODAL_BUTTON}{0ul}")
                .WithStyle(ButtonStyle.Secondary))
            .WithButton(new ButtonBuilder()
                .WithLabel("Edit")
                .WithCustomId($"{ModulePrefixes.CONFIG_FILTERS_EDIT_BUTTON}")
                .WithStyle(ButtonStyle.Secondary)
                .WithDisabled(filters.Length <= 0))
            .WithButton(new ButtonBuilder()
                .WithLabel("Remove")
                .WithCustomId($"{ModulePrefixes.CONFIG_FILTERS_REMOVE_BUTTON}")
                .WithStyle(ButtonStyle.Secondary)
                .WithDisabled(filters.Length <= 0))
            .WithRedButton();

        return new MessageContents("", embed.Build(), components);
    }

    private static SelectMenuBuilder GetFilterSelectMenu(string customId, IEnumerable<RegexFilter> filters)
    {
        return new SelectMenuBuilder()
            .WithCustomId(customId)
            .WithOptions(filters.Select(y =>
                new SelectMenuOptionBuilder().WithLabel(y.Filter).WithDescription(y.Template)
                    .WithValue(y.Id.ToString())).ToList());
    }

    [ComponentInteraction(ModulePrefixes.CONFIG_FILTERS_REMOVE_BUTTON)]
    private async Task RemoveButton()
    {
        await DeferAsync();

        var filters = await mangaService.GetFilters(Context.Guild.Id);

        await ModifyOriginalResponseAsync(x =>
        {
            x.Components = new ComponentBuilder()
                .WithSelectMenu(GetFilterSelectMenu(ModulePrefixes.CONFIG_FILTERS_REMOVE_FILTER_SELECT, filters))
                .WithButton("Back", ModulePrefixes.CONFIG_PAGE_SELECT_PAGE_BUTTON + StateSerializer.SerializeObject(Id), ButtonStyle.Secondary)
                .Build();
        });
    }

    [ComponentInteraction(ModulePrefixes.CONFIG_FILTERS_REMOVE_FILTER_SELECT)]
    private async Task RemoveFilterSelectChanged(string id)
    {
        await DeferAsync();

        var filter = await mangaService.GetFilter(uint.Parse(id), Context.Guild.Id);

        if (filter == null)
        {
            await FollowupAsync("That filter no longer exists anyway!", ephemeral: true);
            return;
        }

        await mangaService.RemoveFilter(filter);

        await ModifyOriginalResponseAsync(await GetMessageContents(new ConfigCommandService.State(page: Id)));
    }

    [ComponentInteraction(ModulePrefixes.CONFIG_FILTERS_EDIT_BUTTON)]
    private async Task EditButton()
    {
        await DeferAsync();

        var filters = await mangaService.GetFilters(Context.Guild.Id);

        await ModifyOriginalResponseAsync(x =>
        {
            x.Components = new ComponentBuilder()
                .WithSelectMenu(GetFilterSelectMenu(ModulePrefixes.CONFIG_FILTERS_EDIT_FILTER_SELECT, filters))
                .WithButton("Back", ModulePrefixes.CONFIG_PAGE_SELECT_PAGE_BUTTON + StateSerializer.SerializeObject(Id), ButtonStyle.Secondary)
                .Build();
        });
    }

    [ComponentInteraction(ModulePrefixes.CONFIG_FILTERS_EDIT_FILTER_SELECT)]
    private async Task EditFilterSelectChanged(string id)
    {
        await DeferAsync();

        var filter = await mangaService.GetFilter(uint.Parse(id), Context.Guild.Id);

        if (filter == null)
        {
            await FollowupAsync("That filter no longer exists!", ephemeral: true);
            return;
        }

        await ModifyOriginalResponseAsync(await UpsertConfirmation(filter));
    }

    [ComponentInteraction(ModulePrefixes.CONFIG_FILTERS_OPEN_MODAL_BUTTON + "*")]
    public async Task OpenModalButton(uint id)
    {
        RegexFilter? filter = null;

        // stupid but i cant get the original message
        if (id != 0ul)
        {
            filter = await mangaService.GetFilter(id, Context.Guild.Id);
            if (filter == null)
            {
                await RespondAsync("That filter no longer exists!", ephemeral: true);
                return;
            }
        }

        await Context.Interaction.RespondWithModalAsync<SetRegexModal>(ModulePrefixes.CONFIG_FILTERS_MODAL + (id != 0ul), modifyModal:
            x =>
            {
                if (filter == null) return;

                x.UpdateTextInput(ModulePrefixes.CONFIG_FILTERS_MODAL_FILTER_TEXTBOX, i =>
                {
                    i.Value = filter.Filter;
                });
                x.UpdateTextInput(ModulePrefixes.CONFIG_FILTERS_MODAL_TEMPLATE_TEXTBOX, i =>
                {
                    i.Value = filter.Template;
                });
            });
    }

    [ModalInteraction(ModulePrefixes.CONFIG_FILTERS_MODAL + "*")]
    public async Task ModalResponse(bool existing, SetRegexModal modal)
    {
        await DeferAsync();

        RegexFilter? filter = null;

        if (existing)
        {
            filter = await GetRegexFilterFromContext();

            filter.Filter = modal.Filter;
            filter.Template = modal.Template;
        }

        filter ??= new RegexFilter(id: 0, guildId: Context.Guild.Id, filter: modal.Filter, template: modal.Template,
            filterType: FilterType.Block, channelFilterScope: ChannelFilterScope.Exclude
            );

        var contents = await UpsertConfirmation(filter);

        await ModifyOriginalResponseAsync(contents);
    }

    [ComponentInteraction(ModulePrefixes.CONFIG_FILTERS_CONFIRMATION_ADD_BUTTON)]
    private async Task ConfirmationAddButton()
    {
        await DeferAsync();

        var filter = await GetRegexFilterFromContext();

        var newId = await mangaService.UpdateOrAddRegexFilter(filter);

        if (newId == 0ul)
        {
            var errorEmbed = new EmbedBuilder()
                .WithDescription("Just noting nothing was actually added or changed as the filter no longer exists.")
                .WithColor(colorProvider.GetErrorEmbedColor());

            await FollowupAsync(new MessageContents(errorEmbed), ephemeral: true);
        }

        await ModifyOriginalResponseAsync(await configCommandService.GetMessageContents(
            new ConfigCommandService.State(page: Id), Context));
    }

    private async Task<MessageContents> UpsertConfirmation(RegexFilter filter)
    {
        var embed = new EmbedBuilder().WithColor(await colorProvider.GetEmbedColor(Context.Guild));

        var channels = filter.RegexChannelEntries;

        embed.WithFields
        (
            new EmbedFieldBuilder()
                .WithName(EMBED_NAME_TEMPLATE)
                .WithValue($"```{filter.Template}```"),
            new EmbedFieldBuilder()
                .WithName(EMBED_NAME_FILTER)
                .WithValue($"```{filter.Filter}```"),
            new EmbedFieldBuilder()
                .WithName(EMBED_NAME_CHANNELS)
                .WithValue(channels
                    .Aggregate("", (current, next) => current + $"<#{next.ChannelId}>{Environment.NewLine}")
                    .StringOrDefault("None!")),
            new EmbedFieldBuilder()
                .WithName(EMBED_NAME_FILTER_TYPE)
                .WithValue(filter.FilterType.Humanize())
                .WithIsInline(true),
            new EmbedFieldBuilder()
                .WithName(EMBED_NAME_SCOPE)
                .WithValue(filter.ChannelFilterScope.Humanize())
                .WithIsInline(true)
        );

        embed.WithFooter(new EmbedFooterBuilder()
            .WithText(filter.Id.ToString()));

        var components = new ComponentBuilder()
            .WithSelectMenu(new SelectMenuBuilder()
                .WithCustomId(ModulePrefixes.CONFIG_FILTERS_CONFIRMATION_FILTER_TYPE)
                .WithOptions(
                    Enum.GetValues<FilterType>().Select(x =>
                        new SelectMenuOptionBuilder()
                            .WithDefault(x == filter.FilterType)
                            .WithLabel("Filter type: " + x.Humanize())
                            .WithDescription(x switch
                            {
                                FilterType.Allow => "If the manga matches the filter, it can be shown.",
                                FilterType.Block => "If the manga matches the filter, it will not be shown.",
                                _ => "looks like i forgot a description??",
                            })
                            .WithValue(StateSerializer.SerializeObject(x))
                            ).ToList()
                    )
                )
            .WithSelectMenu(new SelectMenuBuilder()
                .WithCustomId(ModulePrefixes.CONFIG_FILTERS_CONFIRMATION_CHANNEL_SCOPE)
                .WithOptions(
                    Enum.GetValues<ChannelFilterScope>().Select(x =>
                        new SelectMenuOptionBuilder()
                            .WithDefault(x == filter.ChannelFilterScope)
                            .WithLabel("Channel scope: " + x.Humanize())
                            .WithDescription(x switch
                            {
                                ChannelFilterScope.Include => "The filter will only apply in the specified channels.",
                                ChannelFilterScope.Exclude => "The filter will apply in every channel except the specified channels.",
                                _ => "looks like i forgot a description??",
                            })
                            .WithValue(StateSerializer.SerializeObject(x))
                    ).ToList()
                )
            )
            .WithSelectMenu(new SelectMenuBuilder()
                .WithPlaceholder("Channels")
                .WithType(ComponentType.ChannelSelect)
                .WithChannelTypes(ChannelType.Text, ChannelType.Forum)
                .WithCustomId(ModulePrefixes.CONFIG_FILTERS_CONFIRMATION_CHANNEL_SELECT)
                .WithDefaultValues
                (
                    channels
                        .Select(x => new SelectMenuDefaultValue(x.ChannelId, SelectDefaultValueType.Channel))
                        .ToArray()
                )
                .WithMinValues(0)
                .WithMaxValues(25)
            )
            // Rare case where button state is not used and we're gonna grab from the embed etc instead (100 characters too small for what people probably need)
            .WithButton(new ButtonBuilder()
                .WithLabel("Save")
                .WithCustomId(ModulePrefixes.CONFIG_FILTERS_CONFIRMATION_ADD_BUTTON)
                .WithStyle(ButtonStyle.Success))
            .WithButton(new ButtonBuilder()
                .WithLabel("Edit")
                .WithCustomId(ModulePrefixes.CONFIG_FILTERS_OPEN_MODAL_BUTTON + filter.Id)
                .WithStyle(ButtonStyle.Secondary))
            .WithButton(new ButtonBuilder()
                .WithLabel("Back")
                .WithCustomId(ModulePrefixes.CONFIG_PAGE_SELECT_PAGE_BUTTON +
                              StateSerializer.SerializeObject(StateSerializer.SerializeObject(Id)))

                .WithStyle(ButtonStyle.Danger))
            ;

        return new MessageContents(string.Empty, embed.Build(), components);
    }

    [ComponentInteraction(ModulePrefixes.CONFIG_FILTERS_CONFIRMATION_CHANNEL_SELECT)]
    private async Task UpsertConfirmationChannelSelectChanged(IChannel[] channels)
    {
        await DeferAsync();

        var filter = await GetRegexFilterFromContext(channels.Select(x => new RegexChannelEntry { ChannelId = x.Id}).ToList());

        await ModifyOriginalResponseAsync(await UpsertConfirmation(filter));
    }

    [ComponentInteraction(ModulePrefixes.CONFIG_FILTERS_CONFIRMATION_FILTER_TYPE)]
    private async Task UpsertConfirmationFilterTypeChanged(string id)
    {
        await DeferAsync();

        var filter = await GetRegexFilterFromContext();

        filter.FilterType = StateSerializer.DeserializeObject<FilterType>(id);

        await ModifyOriginalResponseAsync(await UpsertConfirmation(filter));
    }

    [ComponentInteraction(ModulePrefixes.CONFIG_FILTERS_CONFIRMATION_CHANNEL_SCOPE)]
    private async Task UpsertConfirmationChannelScopeChanged(string id)
    {
        await DeferAsync();

        var filter = await GetRegexFilterFromContext();

        filter.ChannelFilterScope = StateSerializer.DeserializeObject<ChannelFilterScope>(id);

        await ModifyOriginalResponseAsync(await UpsertConfirmation(filter));
    }

    private async Task<RegexFilter> GetRegexFilterFromContext(List<RegexChannelEntry>? channels = null)
    {
        var ogRes = await Context.Interaction.GetOriginalResponseAsync();

        var embed = ogRes.Embeds.First(x => x.Type == EmbedType.Rich);

        var template = CompiledRegex.CodeBlockMatcher().Match(embed.Fields.First(x => x.Name == EMBED_NAME_TEMPLATE).Value).Groups[1].Value;
        var filter = CompiledRegex.CodeBlockMatcher().Match(embed.Fields.First(x => x.Name == EMBED_NAME_FILTER).Value).Groups[1].Value;
        var filterType = embed.Fields.First(x => x.Name == EMBED_NAME_FILTER_TYPE).Value.DehumanizeTo<FilterType>();
        var scope = embed.Fields.First(x => x.Name == EMBED_NAME_SCOPE).Value.DehumanizeTo<ChannelFilterScope>();

        channels ??= CompiledRegex.ChannelMatcher().Matches(embed.Fields.First(x => x.Name == EMBED_NAME_CHANNELS).Value).Select(x => new RegexChannelEntry
        {
            ChannelId = ulong.Parse(x.Groups[1].Value)
        }).ToList();

        var idTxt = embed.Footer?.Text ?? throw new InvalidOperationException("Footer missing?");
        var id = uint.Parse(idTxt);

        return new RegexFilter(id: id, guildId: Context.Guild.Id, filter: filter,
            template: template, filterType: filterType, channelFilterScope: scope,
            regexChannelEntries: channels);
    }
}
