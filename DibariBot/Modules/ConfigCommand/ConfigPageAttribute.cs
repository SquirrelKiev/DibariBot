namespace DibariBot.Modules.ConfigCommand;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class ConfigPageAttribute(Page id, string label, string description, Conditions conditions) : Attribute
{
    public Page Id => id;
    public string Label => label;
    public string Description => description;
    public Conditions Conditions => conditions;
}

public enum Page
{
    Help,
    DefaultManga,
    RegexFilters,
    Prefix,
    Appearance,
    MangaAlias
}

[Flags]
public enum Conditions
{
    None = 0,
    NotInDm = 1 << 0,
    PrefixCommandsEnabled = 1 << 1
}