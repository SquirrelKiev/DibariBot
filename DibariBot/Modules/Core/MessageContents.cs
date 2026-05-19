namespace DibariBot;

public struct MessageContents
{
    /// <summary>
    /// Whether the red close button should be added when no component builder is specified.
    /// </summary>
    public static bool AddRedButtonDefault = true;

    public string? body;
    public Embed[]? embeds;
    public MessageComponent? components;

    public MessageContents(string body, Embed[] embeds, ComponentBuilder? components)
    {
        this.body = body;
        this.embeds = embeds;

        if (AddRedButtonDefault)
            components ??= new ComponentBuilder().WithRedButton();
        this.components = components?.Build();
    }

    public MessageContents(string body = "", Embed? embed = null, ComponentBuilder? components = null, bool redButton = true)
    {
        this.body = body;
        embeds = embed == null ? null : [embed];

        if (AddRedButtonDefault && redButton)
            components ??= new ComponentBuilder().WithRedButton();

        if (components != null)
            this.components = components.Build();
    }

    public MessageContents(EmbedBuilder embed, ComponentBuilder? components = null, string body = "", bool redButton = true)
    {
        this.body = body;
        embeds = [embed.Build()];

        if (AddRedButtonDefault && redButton)
            components ??= new ComponentBuilder().WithRedButton();

        if (components != null)
            this.components = components.Build();
    }
    
    public MessageContents(MessageComponent component)
    {
        this.components = component;
    }
}