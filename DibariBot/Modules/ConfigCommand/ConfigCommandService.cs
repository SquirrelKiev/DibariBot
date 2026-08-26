using System.Reflection;

namespace DibariBot.Modules.ConfigCommand;

public class ConfigCommandService(IServiceProvider services, BotConfig botConfig)
{
    public struct State(Page page, string data)
    {
        public Page page = page;
        public string data = data;

        public State()
            : this(default, string.Empty) { }

        public State(Page page)
            : this()
        {
            this.page = page;
            data = string.Empty;
        }
    }

    public ConfigPageDefinition[] ConfigPages
    {
        get { return configPages ??= GetConfigPageDefinitions(); }
    }
    private ConfigPageDefinition[]? configPages;

    public async Task<MessageContents> GetMessageContents(State state, IInteractionContext context)
    {
        var page = ConfigPages.First(x => x.ConfigPageAttribute.Id == state.page);

        var instancedPage = (IConfigPage)ActivatorUtilities.CreateInstance(services, page.PageType);

        var method =
            instancedPage
                .GetType()
                .GetMethod("SetContext", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new NullReferenceException("SetContext doesn't exist!");
        method.Invoke(instancedPage, [context]);

        return await instancedPage.GetMessageContents(state);
    }

    public static ConfigPageDefinition[] GetConfigPageDefinitions()
    {
        var assembly = Assembly.GetExecutingAssembly();

        var types = assembly
            .GetTypes()
            .Select(t => new
            {
                PageType = t,
                ConfigPageAttribute = t.GetCustomAttributes<ConfigPageAttribute>(true)
                    .FirstOrDefault(),
            })
            .Where(t1 => t1.ConfigPageAttribute != null)
            // avoiding nullable issues
            .Select(x => new ConfigPageDefinition
            {
                ConfigPageAttribute = x.ConfigPageAttribute!,
                PageType = x.PageType,
            })
            .OrderBy(x => x.ConfigPageAttribute.Id)
            .ToArray();

        return types;
    }

    public SelectMenuBuilder GetPageSelectDropdown(Page id, bool isDm)
    {
        var dropdown = new SelectMenuBuilder().WithCustomId(ModulePrefixes.CONFIG_PAGE_SELECT_PAGE);

        foreach (var page in FilteredConfigPages(isDm))
        {
            dropdown.AddOption(
                new SelectMenuOptionBuilder()
                    .WithLabel(page.ConfigPageAttribute.Label)
                    .WithValue(StateSerializer.SerializeObject(page.ConfigPageAttribute.Id))
                    .WithDefault(page.ConfigPageAttribute.Id.Equals(id))
                    .WithDescription(page.ConfigPageAttribute.Description)
            );
        }

        return dropdown;
    }

    public IEnumerable<ConfigPageDefinition> FilteredConfigPages(bool isDm) =>
        ConfigPages.Where(page =>
        {
            var passes = true;
            
            if (passes && page.ConfigPageAttribute.Conditions.HasFlag(Conditions.NotInDm))
            {
                passes = !isDm;
            }
            
            if (passes && page.ConfigPageAttribute.Conditions.HasFlag(Conditions.PrefixCommandsEnabled))
            {
                passes = botConfig.PrefixCommandsSupport;
            }

            return passes;
        });
}

public class ConfigPageDefinition
{
    public required Type PageType { get; set; }
    public required ConfigPageAttribute ConfigPageAttribute { get; set; }
}
