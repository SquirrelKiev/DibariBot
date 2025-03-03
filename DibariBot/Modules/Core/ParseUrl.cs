using System.Text.RegularExpressions;

namespace DibariBot.Modules;

// TODO: Convert all of this to source-generated regex.
#pragma warning disable SYSLIB1045

public static class ParseUrl
{
    public static SeriesIdentifier? ParseMangaUrl(string url)
    {
        var seriesIdentifier = new SeriesIdentifier();

        if (Regex.IsMatch(url, @"imgur\."))
        {
            seriesIdentifier.platform = "imgur";
            seriesIdentifier.series = Regex.Match(url, @"(a\/|gallery\/)([A-Z0-9a-z]{5}[A-Z0-9a-z]*\b)").Groups[2].Value;
        }
        else if (Regex.IsMatch(url, @"git\.io"))
        {
            seriesIdentifier.platform = "gist";
            seriesIdentifier.series = Regex.Match(url, @"(git.io\/)(.*)").Groups[2].Value;
        }
        else if (Regex.IsMatch(url, @"(raw|gist)\.githubusercontent"))
        {
            seriesIdentifier.platform = "gist";

            if (!url.StartsWith("http"))
            {
                url = "https://" + url;
            }

            var parsedUrl = new Uri(url);
            var b64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{parsedUrl.Host.Split('.')[0]}{parsedUrl.AbsolutePath}"));
            seriesIdentifier.series = b64.Replace('+', '-').Replace('/', '_').Replace("=", "");
        }
        else if (Regex.IsMatch(url, @"^[0-9]{5}[0-9]?$") ||
                Regex.IsMatch(url, "nhentai") && Regex.IsMatch(url, @"/\b[0-9]+\b"))
        {
            seriesIdentifier.platform = "nhentai";
            seriesIdentifier.series = Regex.Match(url, @"(\/?)(\b[0-9]+\b)").Groups[2].Value;
        }
        else if (Regex.IsMatch(url, @"mangadex\.org\/title"))
        {
            seriesIdentifier.platform = "mangadex";
            seriesIdentifier.series = Regex.Match(url, @"(\/?)([0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12})").Groups[2].Value;
        }
        else if (Regex.IsMatch(url, @"^https:\/\/weebcentral.com\/series\/[a-zA-Z0-9]*\/"))
        {
            seriesIdentifier.platform = "weebcentral";
            seriesIdentifier.series = Regex.Match(url, @"^https:\/\/weebcentral.com\/series\/([a-zA-Z0-9]*)\/").Groups[1].Value;
        }
        else if (Regex.IsMatch(url, @"reddit\.com", RegexOptions.IgnoreCase))
        {
            seriesIdentifier.platform = "reddit";
            seriesIdentifier.series = Regex.Match(url, @"reddit\.com\/(?:r|u(?:ser)?)\/(?:[a-z0-9_\-]+)\/comments\/([a-z0-9]+)", RegexOptions.IgnoreCase)?.Groups[1].Value;

            if (string.IsNullOrEmpty(seriesIdentifier.series))
            {
                seriesIdentifier.series = Regex.Match(url, @"reddit\.com\/gallery\/([a-z0-9]+)", RegexOptions.IgnoreCase)?.Groups[1].Value;
            }
        }
        else if (Regex.IsMatch(url, @"imgchest\.com"))
        {
            seriesIdentifier.platform = "imgchest";
            seriesIdentifier.series = Regex.Match(url, @"p\/(\w+)", RegexOptions.IgnoreCase)?.Groups[1].Value;
        }
        else if (Regex.IsMatch(url, @"cubari\.moe\/read\/api"))
        {
            seriesIdentifier.platform = Regex.Match(url, @"(\/read\/api\/)([^/]+)(\/series\/)([^/]+)").Groups[2].Value;
            seriesIdentifier.series = Regex.Match(url, @"(\/read\/api\/)([^/]+)(\/series\/)([^/]+)").Groups[4].Value;
        }
        else if (Regex.IsMatch(url, @"cubari\.moe\/read"))
        {
            seriesIdentifier.platform = Regex.Match(url, @"(\/read\/)([^/]+)(\/)([^/]+)").Groups[2].Value;
            seriesIdentifier.series = Regex.Match(url, @"(\/read\/)([^/]+)(\/)([^/]+)").Groups[4].Value;
        }
        else if (Regex.IsMatch(url, @"^[a-z]+\/[^/]+$", RegexOptions.IgnoreCase))
        {
            var split = url.Split("/");
            if (split.Length == 2)
            {
                seriesIdentifier.platform = split[0];
                seriesIdentifier.series = split[1];
            }
        }
        else if (url == "xkcd")
        {
            seriesIdentifier.platform = "xkcd";
            seriesIdentifier.series = "none";
        }
        else if (Regex.IsMatch(url, @"p(?:p|h?i)xiv\.net"))
        {
            seriesIdentifier.platform = "pixiv";
            seriesIdentifier.series = Regex.Match(url, @"(?:\/[a-zA-Z0-9]+)?\/artworks\/([a-zA-Z0-9]+)")?.Groups[1].Value;
        }

        if (seriesIdentifier.platform == null || seriesIdentifier.series == null)
            return null;
        
        return seriesIdentifier;
    }
}
