using System.Reflection;
using Discord.WebSocket;
using Humanizer;

namespace DibariBot.Modules.About;

[Inject(ServiceLifetime.Singleton)]
public class AboutService
{
    private Lazy<ProjectInfo> projectInfo;
    private Lazy<Task<ProfileInfo>> profileInfo;

    private readonly DiscordSocketClient discordClient;

    public AboutService(DiscordSocketClient discordClient)
    {
        this.discordClient = discordClient;

        projectInfo = new Lazy<ProjectInfo>(GetProjectInfo);
        profileInfo = new Lazy<Task<ProfileInfo>>(GetProfileInfo);
    }
    
    public void NukeCache()
    {
        projectInfo = new Lazy<ProjectInfo>(GetProjectInfo);
        profileInfo = new Lazy<Task<ProfileInfo>>(GetProfileInfo);
    }

    public async Task<MessageComponent> BuildComponent()
    {
        var components = new ComponentBuilderV2();

        var profile = await profileInfo.Value;

        var container = new ContainerBuilder();
        if (profile.Banner != null)
            container.WithMediaGallery([profile.Banner]);

        var project = projectInfo.Value;
        var versionString = GetFriendlyVersionString(project);

        var text = $"## {profile.Username}\n" +
                   $"Also known as {project.ProjectName}!\n" +
                   $"**Version** {versionString}\n" +
                   $"**By** {project.AuthorDiscordIds?.Split(',').Select(x => $"<@{x}>").Humanize() ?? project.Author}";

        container.WithSection(new SectionBuilder(new ThumbnailBuilder(profile.Avatar), new TextDisplayBuilder(text)));

        components.WithContainer(container);

        return components.Build();
    }

    private static string GetFriendlyVersionString(ProjectInfo projectInfo)
    {
        string versionString;
        if (projectInfo.InformationalVersion == null)
        {
            versionString = "Unknown";
        }
        else if (projectInfo.CommitHash == null || projectInfo.RepositoryUrl == null ||
                 projectInfo.CommitBody == null || projectInfo.CommitDate == 0)
        {
            versionString = projectInfo.InformationalVersion;
        }
        else
        {
            var maxIndex = projectInfo.CommitBody.IndexOf(';');
            var shortBody = maxIndex == -1
                ? projectInfo.CommitBody.Truncate(100)
                : projectInfo.CommitBody[..maxIndex].Truncate(100);

            versionString =
                $"[{projectInfo.InformationalVersion}]({projectInfo.RepositoryUrl}/commit/{projectInfo.CommitHash}) - " +
                $"`{shortBody.Replace("`", "\\`")}` (<t:{projectInfo.CommitDate}:{(char)TimestampTagStyles.Relative}>)";
        }

        return versionString;
    }

    private async Task<ProfileInfo> GetProfileInfo()
    {
        var self = discordClient.Rest.CurrentUser;

        await self.UpdateAsync();

        var username = self.Username;
        var banner = self.GetBannerUrl(ImageFormat.Auto, 4096);
        var avatar = self.GetAvatarUrl(ImageFormat.Auto, 4096);
        
        return new ProfileInfo(username, banner, avatar);
    }

    private static ProjectInfo GetProjectInfo()
    {
        var currentAssembly = Assembly.GetExecutingAssembly();

        var informationalVersion = currentAssembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;
        var projectName = currentAssembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product;
        var author = currentAssembly.GetCustomAttribute<AssemblyCompanyAttribute>()?.Company;
        var metadataAttributes = currentAssembly.GetCustomAttributes<AssemblyMetadataAttribute>().ToList();
        var repositoryUrl = metadataAttributes.FirstOrDefault(x => x.Key == "RepositoryUrl")?.Value;
        var commitHash = metadataAttributes.FirstOrDefault(x => x.Key == "CommitHash")?.Value;
        var authorDiscordIds = metadataAttributes.FirstOrDefault(x => x.Key == "AuthorDiscordIds")?.Value;
        var commitBody = metadataAttributes.FirstOrDefault(x => x.Key == "CommitBody")?.Value;
        if (!ulong.TryParse(metadataAttributes.FirstOrDefault(x => x.Key == "CommitDate")?.Value, out var commitDate))
            commitDate = 0;

        return new ProjectInfo(projectName, informationalVersion, repositoryUrl, commitHash, commitBody, commitDate,
            author, authorDiscordIds);
    }
}

public readonly record struct ProjectInfo(
    string? ProjectName,
    string? InformationalVersion,
    string? RepositoryUrl,
    string? CommitHash,
    string? CommitBody,
    ulong CommitDate,
    string? Author,
    string? AuthorDiscordIds);


public readonly record struct ProfileInfo(string Username, string? Banner, string Avatar);
