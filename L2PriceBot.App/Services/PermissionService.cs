using System;
using Discord.WebSocket;
using L2PriceBot.App.Configuration;
using Microsoft.Extensions.Options;

namespace L2PriceBot.App.Services;

public class PermissionService
{
    private readonly BotSettings _settings;

    public PermissionService(IOptions<BotSettings> settings)
    {
        _settings = settings.Value;
    }

    public bool HasAdminRole(SocketUser user)
    {
        if (user is SocketGuildUser guildUser)
        {
            foreach (var role in guildUser.Roles)
            {
                if (role.Id == _settings.AdminRoleId)
                {
                    return true;
                }
            }
        }
        return false;
    }
}
