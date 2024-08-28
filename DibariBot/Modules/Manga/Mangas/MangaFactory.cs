﻿namespace DibariBot.Modules.Manga;

[Inject(ServiceLifetime.Singleton)]
public class MangaFactory
{
    private readonly IServiceProvider services;

    public MangaFactory(IServiceProvider services)
    {
        this.services = services;
    }

    public async Task<IManga?> GetManga(SeriesIdentifier identifier)
    {
        Type? mangaType;

        // TODO: ideally I dont have to change anything here to add a new manga/platform
        mangaType = identifier.platform switch
        {
            "imgur" or
            "gist" or
            "nhentai" or
            "mangasee" or
            "reddit" or
            "imgchest" => typeof(CubariManga),
            "xkcd" => typeof(XkcdManga),
            "mangadex" => typeof(MangaDexManga),
            "pixiv" => typeof(PhixivManga),
            _ => null,
        };

        if (mangaType == null)
        {
            return null;
        }

        return await ((IManga)services.GetRequiredService(mangaType)).Initialize(identifier);
    }
}
