namespace L2PriceBot.App.Configuration;

public class BotSettings
{
    public string DiscordToken { get; set; } = string.Empty;
    public ulong GuildId { get; set; }
    public ulong AdminRoleId { get; set; }
    public string DataPath { get; set; } = "data/items.json";
    public string Timezone { get; set; } = "America/Argentina/Buenos_Aires";
}
