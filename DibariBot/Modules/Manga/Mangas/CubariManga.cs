using DibariBot.Apis;
using Newtonsoft.Json.Linq;

namespace DibariBot.Modules.Manga;

[Inject(Microsoft.Extensions.DependencyInjection.ServiceLifetime.Transient)]
public class CubariManga : IManga
{
    public MangaMetadata Metadata { get; protected set; }
    public Dictionary<string, string> Groups { get; protected set; }

    protected SeriesIdentifier identifier;
    protected SortedList<string, CubariChapterSchema> chapters;

    protected readonly CubariApi cubari;

#pragma warning disable CS8618 // null error, technically true but only if Initialize() wasn't called which in that case, it should throw anyway
    public CubariManga(CubariApi cubari)
    {
        this.cubari = cubari;
    }
#pragma warning restore CS8618

    public virtual async Task<IManga> Initialize(SeriesIdentifier id)
    {
        ArgumentNullException.ThrowIfNull(id.platform, nameof(id.platform));
        ArgumentNullException.ThrowIfNull(id.series, nameof(id.series));

        string url =
            $"read/api/{Uri.EscapeDataString(id.platform)}/series/{Uri.EscapeDataString(id.series)}";

        var mangaRes = await cubari.Get<CubariMangaSchema>(
            url,
            new CacheValueSettings(TimeSpan.FromMinutes(15))
        );

        Metadata = new MangaMetadata
        {
            title = mangaRes.title,
            description = mangaRes.description,
            author = mangaRes.author,
            artist = mangaRes.artist,
            tags = Array.Empty<string>(),
            contentRating =
                id.platform == "nhentai"
                    ? MangaAttributesSchema.ContentRating.Pornographic
                    : MangaAttributesSchema.ContentRating.Unknown,
        };
        Groups = mangaRes.groups;

        chapters = new SortedList<string, CubariChapterSchema>(
            mangaRes.chapters,
            new ChapterNameComparer()
        );

        this.identifier = new SeriesIdentifier(id.platform, mangaRes.slug);

        return this;
    }

    public virtual SeriesIdentifier GetIdentifier()
    {
        return identifier;
    }

    public virtual Task<MangaMetadata> GetMetadata()
    {
        return Task.FromResult(Metadata);
    }

    public virtual string GetUrl(Bookmark bookmark)
    {
        return cubari.GetUrl(identifier, bookmark);
    }

    /// <exception cref="NotSupportedException">If the type we get from the api is something we don't handle</exception>
    public virtual async Task<ChapterSrcs> GetImageSrcs(string chapter)
    {
        var chapterObj = chapters[chapter];

        var group = chapterObj.groups.First();

        var srcs = await GetImageSrcsFromGroup(group.Value);

        return new(srcs, Groups[group.Key]);
    }

    protected virtual async Task<string[]> GetImageSrcsFromGroup(JToken token)
    {
        ArgumentNullException.ThrowIfNull(token, nameof(token));

        // sure hope no one does any weird recursion nonsense :clueless:
        // wait that might actually be an issue that could crop up
        // TODO: protect against weird recursion nonsense
        if (token.Type == JTokenType.String)
        {
            string url = token.ToString();

            var req = await cubari.Get<JToken>(url);

            return await GetImageSrcsFromGroup(req!);
        }
        else if (token.Type == JTokenType.Array)
        {
            if (token.All(t => t.Type == JTokenType.String))
            {
                string[] imageUrls = token.ToObject<string[]>()!;

                return imageUrls;
            }
            else if (token.All(t => t.Type == JTokenType.Object))
            {
                CubariImgurSchema[] imgurObjects = token.ToObject<CubariImgurSchema[]>()!;

                return imgurObjects.Select((x) => x.src).ToArray();
            }
        }

        // no clue what on earth we've received, bail
        throw new NotSupportedException();
    }

    public virtual Task<ChapterMetadata> GetChapterMetadata(string chapter)
    {
        var chapterData = chapters[chapter];

        return Task.FromResult(
            new ChapterMetadata()
            {
                id = chapter,
                title = chapterData.title,
                volume = chapterData.volume,
            }
        );
    }

    public virtual Task<string?> GetNextChapterKey(string currentChapterKey)
    {
        var nextChapterIndex = chapters.IndexOfKey(currentChapterKey) + 1;

        if (nextChapterIndex >= chapters.Count)
        {
            return Task.FromResult<string?>(null);
        }

        return Task.FromResult<string?>(chapters.GetKeyAtIndex(nextChapterIndex));
    }

    public virtual Task<string?> GetPreviousChapterKey(string currentChapterKey)
    {
        var previousChapterKey = chapters.IndexOfKey(currentChapterKey) - 1;

        if (previousChapterKey < 0)
        {
            return Task.FromResult<string?>(null);
        }

        return Task.FromResult<string?>(chapters.GetKeyAtIndex(previousChapterKey));
    }

    public virtual Task<string?> DefaultChapter()
    {
        return Task.FromResult(chapters.Keys.FirstOrDefault());
    }

    public virtual Task<bool> HasChapter(string chapter)
    {
        return Task.FromResult(chapters.ContainsKey(chapter));
    }
}

public struct CubariMangaSchema
{
    public string slug,
        title,
        description,
        author,
        artist,
        cover,
        series_name;
    public Dictionary<string, string> groups;
    public Dictionary<string, CubariChapterSchema> chapters;
}

public struct CubariChapterSchema
{
    public string volume;
    public string title;
    public Dictionary<string, JToken> groups;

    public Dictionary<string, long> release_date;

    // [JsonConverter(typeof(UnixDateTimeConverter))]
    public long last_updated;
}

public struct CubariImgurSchema
{
    public string description,
        src;
}

public struct ChapterSrcs
{
    public string[] srcs;
    public string group;

    public ChapterSrcs(string[] srcs, string group)
    {
        ArgumentNullException.ThrowIfNull(srcs, nameof(srcs));

        this.srcs = srcs;
        this.group = group;
    }
}
