using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using L2PriceBot.App.Configuration;
using L2PriceBot.App.Services;
using L2PriceBot.App.Utils;
using Microsoft.Extensions.Options;

namespace L2PriceBot.App.Commands;

public class PriceCommands : InteractionModuleBase<SocketInteractionContext>
{
    private readonly PriceService _priceService;
    private readonly PermissionService _permissionService;
    private readonly LoggingService _loggingService;
    private readonly BotSettings _settings;

    public PriceCommands(
        PriceService priceService, 
        PermissionService permissionService,
        LoggingService loggingService,
        IOptions<BotSettings> settings)
    {
        _priceService = priceService;
        _permissionService = permissionService;
        _loggingService = loggingService;
        _settings = settings.Value;
    }

    private bool CheckPermissions()
    {
        return _permissionService.HasAdminRole(Context.User);
    }

    [SlashCommand("precios", "Muestra la lista completa de precios actualmente cargada.")]
    public async Task GetPreciosAsync()
    {
        await DeferAsync();

        var items = await _priceService.GetAllItemsAsync();

        if (!items.Any())
        {
            await FollowupAsync("❌ No hay precios cargados todavía.");
            return;
        }

        var groupedByCategory = items.GroupBy(i => i.Category).ToList();

        var embed = new EmbedBuilder()
            .WithTitle("🏪 LISTA DE PRECIOS L2 SUDAMÉRICA")
            .WithDescription("💰 Todos los precios están expresados en DONATOR COINS (DC)")
            .WithColor(Color.Gold);

        foreach (var categoryGroup in groupedByCategory)
        {
            var sb = new StringBuilder();
            foreach (var item in categoryGroup)
            {
                sb.AppendLine($"{item.Name} — {PriceFormatter.FormatDC(item.Price)}");
            }
            embed.AddField($"⚔️ {categoryGroup.Key}", $"━━━━━━━━━━━━━━━━\n{sb.ToString()}");
        }

        var mostRecentUpdate = items.Max(i => i.UpdatedAt);
        var localTime = TimezoneUtils.GetLocalTime(mostRecentUpdate, _settings.Timezone);
        
        embed.WithFooter($"🕐 Última actualización: {localTime.ToString("dd/MM/yyyy HH:mm")}");

        await FollowupAsync(embed: embed.Build());
    }

    [SlashCommand("precio", "Busca un item y muestra su precio")]
    public async Task GetPrecioAsync([Summary("item", "Nombre del item a buscar")] string itemName)
    {
        await DeferAsync();

        var item = await _priceService.GetItemByNameAsync(itemName);
        
        if (item == null)
        {
            var items = await _priceService.SearchItemsAsync(itemName);
            if (items.Any())
            {
                if (items.Count == 1)
                {
                    item = items.First();
                }
                else
                {
                    var partials = string.Join("\n", items.Take(10).Select(i => $"• {i.Name}"));
                    await FollowupAsync($"❌ No encontré un item con ese nombre exacto. ¿Quisiste decir alguno de estos?\n{partials}");
                    return;
                }
            }
            else
            {
                await FollowupAsync("❌ No encontré ese item.");
                return;
            }
        }

        var localTime = TimezoneUtils.GetLocalTime(item.UpdatedAt, _settings.Timezone);
        
        var embed = new EmbedBuilder()
            .WithTitle(item.Name)
            .WithDescription($"💰 Precio: **{PriceFormatter.FormatDC(item.Price)}**")
            .AddField("Categoría", item.Category, true)
            .WithFooter($"🕐 Actualizado: {localTime.ToString("dd/MM/yyyy HH:mm")}")
            .WithColor(Color.Blue);

        await FollowupAsync(embed: embed.Build());
    }

    [SlashCommand("precio-agregar", "Agrega un nuevo precio (Solo Administradores)")]
    public async Task AddPrecioAsync(
        [Summary("nombre", "Nombre del item")] string nombre, 
        [Summary("precio", "Precio en DC (ej: 1500, 1.5k, etc)")] string precioStr, 
        [Summary("categoria", "Categoría del item")] string categoria)
    {
        await DeferAsync(ephemeral: true); // Hidden from others

        if (!CheckPermissions())
        {
            await FollowupAsync("❌ No tenés permisos para modificar precios.", ephemeral: true);
            return;
        }

        try
        {
            int precio = PriceFormatter.ParseDC(precioStr);
            var item = await _priceService.AddItemAsync(nombre, precio, categoria, Context.User.Username);
            await _loggingService.LogAuditAsync(Context.User.Username, "ADD", nombre, $"Added {PriceFormatter.FormatDC(precio)}");
            await FollowupAsync($"✅ Precio agregado correctamente:\n{item.Name} — {PriceFormatter.FormatDC(item.Price)}", ephemeral: true);
        }
        catch (Exception ex)
        {
             await FollowupAsync($"❌ {ex.Message}", ephemeral: true);
        }
    }

    [SlashCommand("precio-actualizar", "Actualiza un precio existente (Solo Administradores)")]
    public async Task UpdatePrecioAsync(
        [Summary("item", "Nombre exacto del item")] string nombre, 
        [Summary("precio", "Nuevo precio en DC")] string precioStr)
    {
        await DeferAsync(ephemeral: true);

        if (!CheckPermissions())
        {
            await FollowupAsync("❌ No tenés permisos para modificar precios.", ephemeral: true);
            return;
        }

        try
        {
            int precio = PriceFormatter.ParseDC(precioStr);
            var existing = await _priceService.GetItemByNameAsync(nombre);
            if (existing == null)
            {
               await FollowupAsync("❌ No encontré ese item.", ephemeral: true);
               return; 
            }
            var oldPrice = existing.Price;
            var item = await _priceService.UpdateItemAsync(nombre, precio, Context.User.Username);
            await _loggingService.LogAuditAsync(Context.User.Username, "UPDATE", nombre, $"Changed from {PriceFormatter.FormatDC(oldPrice)} to {PriceFormatter.FormatDC(precio)}");
            await FollowupAsync($"✅ Precio actualizado correctamente:\n{item.Name} — {PriceFormatter.FormatDC(oldPrice)} ➡️ {PriceFormatter.FormatDC(item.Price)}", ephemeral: true);
        }
        catch (Exception ex)
        {
             await FollowupAsync($"❌ {ex.Message}", ephemeral: true);
        }
    }

    [SlashCommand("precio-eliminar", "Elimina un precio (Solo Administradores)")]
    public async Task DeletePrecioAsync(
        [Summary("item", "Nombre exacto del item a eliminar")] string nombre)
    {
        await DeferAsync(ephemeral: true);

        if (!CheckPermissions())
        {
            await FollowupAsync("❌ No tenés permisos para modificar precios.", ephemeral: true);
            return;
        }

        try
        {
            await _priceService.DeleteItemAsync(nombre);
            await _loggingService.LogAuditAsync(Context.User.Username, "DELETE", nombre, "Deleted");
            await FollowupAsync($"✅ Item eliminado correctamente.", ephemeral: true);
        }
        catch (Exception ex)
        {
             await FollowupAsync($"❌ {ex.Message}", ephemeral: true);
        }
    }
    
    [SlashCommand("precio-categorias", "Muestra las categorías existentes")]
    public async Task CategoriasAsync()
    {
         await DeferAsync();
         var items = await _priceService.GetAllItemsAsync();
         var categories = items.Select(i => i.Category).Distinct().OrderBy(c => c).ToList();
         
         if (!categories.Any())
         {
             await FollowupAsync("❌ No hay categorías cargadas.");
             return;
         }
         
         var embed = new EmbedBuilder()
            .WithTitle("📋 Categorías")
            .WithColor(Color.Green);
            
         foreach(var cat in categories)
         {
             var count = items.Count(i => i.Category == cat);
             embed.AddField($"⚔️ {cat}", $"{count} items", true);
         }
         
         await FollowupAsync(embed: embed.Build());
    }

    [SlashCommand("precio-buscar", "Busca coincidencias parciales de un item")]
    public async Task BuscarAsync([Summary("item", "Texto a buscar en el nombre del item")] string texto)
    {
         await DeferAsync();
         var items = await _priceService.SearchItemsAsync(texto);
         
         if (!items.Any())
         {
             await FollowupAsync("❌ No encontré ningún item con ese texto.");
             return;
         }
         
         var embed = new EmbedBuilder()
            .WithTitle("🔎 Resultados de búsqueda")
            .WithColor(Color.DarkBlue);
            
         var sb = new StringBuilder();
         foreach(var item in items.Take(20))
         {
             sb.AppendLine($"• **{item.Name}** — {PriceFormatter.FormatDC(item.Price)}");
         }
         
         if (items.Count > 20)
         {
             sb.AppendLine($"\n*Y {items.Count - 20} coincidencias más...*");
         }
         
         embed.WithDescription(sb.ToString());
         await FollowupAsync(embed: embed.Build());
    }

    [SlashCommand("ayuda", "Muestra la explicación de los comandos disponibles")]
    public async Task AyudaAsync()
    {
         await DeferAsync();
         
         var embed = new EmbedBuilder()
            .WithTitle("ℹ️ Comandos Disponibles")
            .WithColor(Color.Teal);
            
         embed.AddField("Usuarios Normales", 
            "`/precios` - Muestra la lista completa de precios.\n" +
            "`/precio item:<nombre>` - Muestra el precio de un item exacto.\n" +
            "`/precio-buscar item:<texto>` - Busca coincidencias parciales.\n" +
            "`/precio-categorias` - Muestra las categorías existentes.\n" +
            "`/ayuda` - Muestra esta ayuda.");
            
         if (CheckPermissions())
         {
             embed.AddField("Administradores",
                "`/precio-agregar nombre:<nombre> precio:<dc> categoria:<cat>` - Agrega un item.\n" +
                "`/precio-actualizar item:<nombre> precio:<dc>` - Actualiza precio.\n" +
                "`/precio-eliminar item:<nombre>` - Elimina un item.");
         }
         
         await FollowupAsync(embed: embed.Build());
    }
}
