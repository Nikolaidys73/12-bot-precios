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

using System.Net.Http;
using System.IO;
using System.Collections.Generic;
using L2PriceBot.App.Models;

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
            int precio = PriceFormatter.ParseDCToInteger(precioStr);
            var item = await _priceService.AddItemAsync(nombre, precio.ToString(), categoria, Context.User.Username);
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
            int precio = PriceFormatter.ParseDCToInteger(precioStr);
            var existing = await _priceService.GetItemByNameAsync(nombre);
            if (existing == null)
            {
               await FollowupAsync("❌ No encontré ese item.", ephemeral: true);
               return; 
            }
            var oldPrice = existing.Price;
            var item = await _priceService.UpdateItemAsync(nombre, precio.ToString(), Context.User.Username);
            await _logging_service.LogAuditAsync(Context.User.Username, "UPDATE", nombre, $"Changed from {PriceFormatter.FormatDC(oldPrice)} to {PriceFormatter.FormatDC(precio)}");
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
                "`/precio-eliminar item:<nombre>` - Elimina un item.\n" +
                "`/importar-excel archivo:<excel>` - Actualiza todos los precios masivamente.");
         }
         
         await FollowupAsync(embed: embed.Build());
    }

    [SlashCommand("setup-canales", "Crea los canales para sugerencias y listas de precios (Solo Administradores)")]
    public async Task SetupCanalesAsync()
    {
        await DeferAsync(ephemeral: true);

        if (!CheckPermissions())
        {
            await FollowupAsync("❌ No tenés permisos para ejecutar este comando.", ephemeral: true);
            return;
        }

        var guild = Context.Guild;
        
        try
        {
            var adminRole = guild.GetRole(_settings.AdminRoleId);
            var everyoneRole = guild.EveryoneRole;

            // Categoría
            var category = await guild.CreateCategoryChannelAsync("PRECIOS L2");

            // Canal 1: Lista de Precios
            var listChannel = await guild.CreateTextChannelAsync("lista-de-precios", prop => 
            {
                prop.CategoryId = category.Id;
                prop.Topic = "Ver y buscar precios del servidor. (Solo lectura)";
            });

            // Permisos canal 1: 
            // Todos: Pueden ver, pero no hablar.
            await listChannel.AddPermissionOverwriteAsync(everyoneRole, new OverwritePermissions(viewChannel: PermValue.Allow, sendMessages: PermValue.Deny));
            // Bot: Puede escribir
            var botRole = guild.CurrentUser.Roles.FirstOrDefault(r => r.IsManaged); // Assuming the bot has a managed role, otherwise use user id
            if (botRole != null)
                await listChannel.AddPermissionOverwriteAsync(botRole, new OverwritePermissions(sendMessages: PermValue.Allow));
            else
                await listChannel.AddPermissionOverwriteAsync(guild.CurrentUser, new OverwritePermissions(sendMessages: PermValue.Allow));

            // Canal 2: Actualizacion de Precios
            var updateChannel = await guild.CreateTextChannelAsync("actualizacion-de-precios", prop => 
            {
                prop.CategoryId = category.Id;
                prop.Topic = "Canal privado para administradores para agregar/quitar/actualizar precios.";
            });

            // Permisos canal 2:
            // Todos: NO VEN EL CANAL
            await updateChannel.AddPermissionOverwriteAsync(everyoneRole, new OverwritePermissions(viewChannel: PermValue.Deny));
            // Admin: Pueden ver y hablar
            if (adminRole != null)
            {
                await updateChannel.AddPermissionOverwriteAsync(adminRole, new OverwritePermissions(viewChannel: PermValue.Allow, sendMessages: PermValue.Allow));
            }

            // Enviar mensajes iniciales automáticos
            await listChannel.SendMessageAsync("🏪 **¡Bienvenidos a la Lista de Precios!** 🏪\n\n- Usa `/precios` para ver el catálogo completo.\n- Usa `/precio item:<nombre>` para ver un ítem específico.\n- Usa `/precio-buscar item:<texto>` para buscar.\n\n*Nota: Los precios están expresados en Donator Coins (DC).*");
            
            await updateChannel.SendMessageAsync($"🔒 **Canal privado de Administración**\n\nAquí los administradores pueden gestionar los precios. Comandos útiles:\n- `/precio-agregar nombre:<nombre> precio:<1500> categoria:<cat>`\n- `/precio-actualizar item:<nombre> precio:<1750>`\n- `/precio-eliminar item:<nombre>`\n- `/importar-excel archivo:<excel>` para actualizar masivamente\n\n*Nota: Las acciones quedan registradas en el log interno.*");

            await FollowupAsync($"✅ Canales creados exitosamente en la categoría 'PRECIOS L2'.", ephemeral: true);
        }
        catch (Exception ex)
        {
             await FollowupAsync($"❌ Hubo un error al crear los canales: {ex.Message}. Verifica que el bot tenga el permiso 'Manage Channels' en el servidor.", ephemeral: true);
        }
    }
    [SlashCommand("importar-excel", "Actualiza la lista de precios masivamente subiendo un archivo CSV (Solo Administradores)")]
    public async Task ImportarExcelAsync(
        [Summary("archivo", "Archivo CSV con la lista de precios")] IAttachment archivoExcel)
    {
        await DeferAsync(ephemeral: true);

        if (!CheckPermissions())
        {
            await FollowupAsync("❌ No tenés permisos para ejecutar este comando.", ephemeral: true);
            return;
        }

        if (!archivoExcel.Filename.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
        {
            await FollowupAsync("❌ Por favor, sube un archivo CSV válido (.csv).", ephemeral: true);
            return;
        }

        try
        {
            using var httpClient = new HttpClient();
            var bytes = await httpClient.GetByteArrayAsync(archivoExcel.Url);
            var csvContent = System.Text.Encoding.UTF8.GetString(bytes);
            
            var newItems = new List<ItemPrice>();
            // Leer líneas
            var lines = csvContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            
            bool isFirstLine = true;
            foreach (var rawLine in lines)
            {
                var line = rawLine.Replace("\"", "").Trim();
                if (string.IsNullOrWhiteSpace(line)) continue;

                // Ignorar encabezado asumiendo que contiene "Nombre"
                if (isFirstLine && line.StartsWith("Nombre", StringComparison.InvariantCultureIgnoreCase))
                {
                    isFirstLine = false;
                    continue;
                }
                isFirstLine = false;
                
                char separator = line.Contains(';') ? ';' : ',';
                var parts = line.Split(separator);
                if (parts.Length < 2) continue;

                string nombre = parts[0];
                string precioStr = parts[1];
                string categoria = parts.Length > 2 ? parts[2] : "General";

                if (string.IsNullOrWhiteSpace(nombre))
                    continue;

                int precio;
                try
                {
                    precio = PriceFormatter.ParseDCToInteger(precioStr);
                }
                catch
                {
                    throw new Exception($"El item '{nombre}' tiene un precio inválido: '{precioStr}'");
                }
                
                newItems.Add(new ItemPrice
                {
                    Name = nombre.Trim(),
                    Price = precio.ToString(),
                    Category = string.IsNullOrWhiteSpace(categoria) ? "General" : categoria.Trim().ToUpper(),
                    UpdatedAt = DateTime.UtcNow,
                    UpdatedBy = Context.User.Username
                });
            }

            if (newItems.Any())
            {
                await _priceService.ReplaceAllItemsAsync(newItems);
                await _loggingService.LogAuditAsync(Context.User.Username, "IMPORT CSV", "Varios", $"Importados {newItems.Count} items.");
                await FollowupAsync($"✅ Se actualizaron correctamente **{newItems.Count}** precios en la base de datos.", ephemeral: true);
            }
            else
            {
                await FollowupAsync("⚠️ El archivo CSV parecía estar vacío o no tener el formato correcto.", ephemeral: true);
            }
        }
        catch (Exception ex)
        {
            await FollowupAsync($"❌ Hubo un error al procesar el archivo CSV: {ex.Message}", ephemeral: true);
        }
    }
}
