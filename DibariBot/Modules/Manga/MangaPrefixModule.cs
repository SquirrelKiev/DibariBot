﻿using Discord.Commands;

namespace DibariBot.Modules.Manga;

public class MangaPrefixModule : DibariPrefixModule
{
    [NamedArgumentType]
    public class NameableArguments
    {
        public string Url { get; set; } = "";
        public string Ch { get; set; } = "";
        public int Pg { get; set; } = 1;
    }

    private readonly MangaService mangaHandler;

    public MangaPrefixModule(MangaService mangaHandler)
    {
        this.mangaHandler = mangaHandler;
    }

    [Command("manga"), Priority(10)]
    [Alias("m")]
    public Task MangaCommand(string url, string chapter, int page) => MangaCommandImpl(url, chapter, page);

    [Command("manga"), Priority(10)]
    [Alias("m")]
    public Task MangaCommand(string chapter, int page) => MangaCommandImpl(chapter: chapter, page: page);

    [Command("manga"), Priority(10)]
    [Alias("m")]
    public Task MangaCommand(string chapter) => MangaCommandImpl(chapter: chapter);

    [Command("manga"), Priority(10)]
    [Alias("m")]
    public Task MangaCommand(NameableArguments namedArgs) => MangaCommandImpl(namedArgs.Url, namedArgs.Ch, namedArgs.Pg);

    [Command("manga"), Priority(0)]
    [Alias("m")]
    private async Task MangaCommandImpl(string url = "", string chapter = "", int page = 1)
    {
        await DeferAsync();

        var contents = await mangaHandler.MangaCommand(Context.Guild?.Id ?? 0ul, GetParentChannel().Id,
            url, chapter, page);

        await ReplyAsync(contents);
    }
}
