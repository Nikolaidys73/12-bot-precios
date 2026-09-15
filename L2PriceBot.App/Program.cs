using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using L2PriceBot.App.Configuration;
using L2PriceBot.App.Repositories;
using L2PriceBot.App.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace L2PriceBot.App;

public class Program
{
    public static async Task Main(string[] args)
    {
        var host = Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((context, config) =>
            {
                config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
                config.AddEnvironmentVariables();
            })
            .ConfigureServices((context, services) =>
            {
                var config = context.Configuration;
                
                // Read from Env vars if available, otherwise fallback to appsettings
                services.Configure<BotSettings>(options =>
                {
                    options.DiscordToken = Environment.GetEnvironmentVariable("DISCORD_TOKEN") ?? config["BotSettings:DiscordToken"] ?? "";
                    options.GuildId = ulong.TryParse(Environment.GetEnvironmentVariable("DISCORD_GUILD_ID") ?? config["BotSettings:GuildId"], out var gId) ? gId : 0;
                    options.AdminRoleId = ulong.TryParse(Environment.GetEnvironmentVariable("ADMIN_ROLE_ID") ?? config["BotSettings:AdminRoleId"], out var rId) ? rId : 0;
                    options.DataPath = Environment.GetEnvironmentVariable("DATA_PATH") ?? config["BotSettings:DataPath"] ?? "data/items.json";
                    options.Timezone = Environment.GetEnvironmentVariable("TIMEZONE") ?? config["BotSettings:Timezone"] ?? "America/Argentina/Buenos_Aires";
                });

                services.AddSingleton(new DiscordSocketClient(new DiscordSocketConfig
                {
                    GatewayIntents = GatewayIntents.None // We only need interactions (slash commands)
                }));
                
                services.AddSingleton(x => new InteractionService(x.GetRequiredService<DiscordSocketClient>()));

                services.AddSingleton<LoggingService>();
                services.AddSingleton<PermissionService>();
                services.AddSingleton<IPriceRepository, JsonPriceRepository>();
                services.AddSingleton<PriceService>();
                services.AddHostedService<BotWorker>();
            })
            .Build();

        await host.RunAsync();
    }
}

public class BotWorker : IHostedService
{
    private readonly DiscordSocketClient _client;
    private readonly InteractionService _interactions;
    private readonly IServiceProvider _services;
    private readonly IOptions<BotSettings> _settings;
    private readonly LoggingService _logger;

    public BotWorker(
        DiscordSocketClient client,
        InteractionService interactions,
        IServiceProvider services,
        IOptions<BotSettings> settings,
        LoggingService logger)
    {
        _client = client;
        _interactions = interactions;
        _services = services;
        _settings = settings;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var token = _settings.Value.DiscordToken;
        if (string.IsNullOrWhiteSpace(token))
        {
            Console.WriteLine("CRITICAL ERROR: Discord Token is missing!");
            return;
        }

        _client.Log += LogAsync;
        _interactions.Log += LogAsync;
        _client.Ready += ReadyAsync;

        await _client.LoginAsync(TokenType.Bot, token);
        await _client.StartAsync();

        // Register interaction modules
        await _interactions.AddModulesAsync(Assembly.GetEntryAssembly(), _services);

        _client.InteractionCreated += HandleInteraction;
    }

    private async Task HandleInteraction(SocketInteraction interaction)
    {
        try
        {
            var ctx = new SocketInteractionContext(_client, interaction);
            await _interactions.ExecuteCommandAsync(ctx, _services);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            if (interaction.Type == InteractionType.ApplicationCommand)
                await interaction.GetOriginalResponseAsync().ContinueWith(async (msg) => await msg.Result.DeleteAsync());
        }
    }

    private async Task ReadyAsync()
    {
        var guildId = _settings.Value.GuildId;
        if (guildId != 0)
        {
            await _interactions.RegisterCommandsToGuildAsync(guildId);
            Console.WriteLine($"Registered commands to Guild {guildId}");
        }
        else
        {
            await _interactions.RegisterCommandsGloballyAsync();
            Console.WriteLine("Registered commands globally");
        }
        
        Console.WriteLine("Bot is ready!");
    }

    private Task LogAsync(LogMessage msg)
    {
        Console.WriteLine(msg.ToString());
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _client.StopAsync();
    }
}
