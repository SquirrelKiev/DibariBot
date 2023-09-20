﻿namespace DibariBot.Modules.MDSearch;

[Inject(Microsoft.Extensions.DependencyInjection.ServiceLifetime.Singleton)]
public class SearchService
{
    public struct State
    {
        public string query;
        public int page;
    }

    private readonly MangaDexApi mangaDexApi;
    private readonly BotConfig config;

    public SearchService(MangaDexApi mdapi, BotConfig config)
    {
        mangaDexApi = mdapi;
        this.config = config;
    }

    public async Task<MessageContents> GetMessageContents(State state)
    {
        var res = await mangaDexApi.GetMangas(new Apis.MangaListQueryParams
        {
            limit = config.MangaDexSearchLimit,
            offset = state.page * config.MangaDexSearchLimit,
            order = new Apis.MangaListQueryOrder()
            {
                relevance = Apis.MangaListQueryOrder.QueryOrderSchema.Descending
            },
            title = state.query
        });

        var totalPages = res.total / config.MangaDexSearchLimit;

        if(totalPages <= 0)
        {
            var errorEmbed = new EmbedBuilder()
                .WithDescription("No results found!");

            return new MessageContents(string.Empty, errorEmbed.Build(), null);
        }

        var embed = new EmbedBuilder();

        foreach (var mangaSchema in res.data)
        {
            var manga = MangaDexApi.MangaSchemaToMetadata(mangaSchema);

            // why cant field titles have URLs
            embed
                .AddField(new EmbedFieldBuilder()
                    .WithName($"{manga.title
                        .StringOrDefault("No title (why?)")
                        .Truncate(config.MaxTitleLength)} by {manga.author.Truncate(config.MaxTitleLength)}"
                        )
                    .WithValue(manga.description
                        .StringOrDefault("No description.")
                        .Truncate(config.MaxDescriptionLength)
                        + $" [(link)]({config.MangaDexSearchUrl.Replace("{{ID}}", mangaSchema.id)})"
                        )
                    )
                .WithFooter(new EmbedFooterBuilder()
                    .WithText($"Page {state.page + 1}/{totalPages}"));
        }

        bool disableLeft = state.page <= 0;
        bool disableRight = state.page >= totalPages - 1;

        var components = new ComponentBuilder()
            .WithSelectMenu(new SelectMenuBuilder()
                    .WithOptions(res.data.Select(x =>
                        new SelectMenuOptionBuilder()
                            .WithValue(x.id.ToString())
                            .WithLabel(x.attributes.title.ToString())
                    ).ToList())
                    .WithCustomId(ModulePrefixes.MANGADEX_SEARCH_DROPDOWN_PREFIX)
                )
            .WithButton(new ButtonBuilder()
                    .WithLabel("<")
                    .WithCustomId(ModulePrefixes.MANGADEX_SEARCH_BUTTON_PREFIX +
                    StateSerializer.SerializeObject(new State()
                    {
                        page = state.page - 1,
                        query = state.query
                    }))
                    .WithDisabled(disableLeft)
                    .WithStyle(config.PrimaryButtonStyle)
                )
            .WithButton(new ButtonBuilder()
                    .WithLabel(">")
                    .WithCustomId(ModulePrefixes.MANGADEX_SEARCH_BUTTON_PREFIX +
                    StateSerializer.SerializeObject(new State()
                    {
                        page = state.page + 1,
                        query = state.query
                    }))
                    .WithDisabled(disableRight)
                    .WithStyle(config.PrimaryButtonStyle)
                )
            .WithRedButton();

        return new MessageContents(string.Empty, embed: embed.Build(), components);
    }
}
