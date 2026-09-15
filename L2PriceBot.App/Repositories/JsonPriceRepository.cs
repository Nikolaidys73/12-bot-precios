using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using L2PriceBot.App.Configuration;
using L2PriceBot.App.Models;
using Microsoft.Extensions.Options;

namespace L2PriceBot.App.Repositories;

public class JsonPriceRepository : IPriceRepository
{
    private readonly BotSettings _settings;
    private readonly string _filePath;
    private readonly object _lock = new object();

    public JsonPriceRepository(IOptions<BotSettings> settings)
    {
        _settings = settings.Value;
        
        var path = Environment.GetEnvironmentVariable("DATA_PATH");
        if (string.IsNullOrWhiteSpace(path))
        {
            path = _settings.DataPath;
        }
        
        _filePath = Path.GetFullPath(path);
    }

    private async Task<List<ItemPrice>> ReadFileAsync()
    {
        if (!File.Exists(_filePath))
        {
            return new List<ItemPrice>();
        }

        try
        {
            using var fileStream = new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var items = await JsonSerializer.DeserializeAsync<List<ItemPrice>>(fileStream) ?? new List<ItemPrice>();
            return items;
        }
        catch
        {
            return new List<ItemPrice>();
        }
    }

    private async Task WriteFileAsync(List<ItemPrice> items)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = _filePath + ".tmp";
        
        using (var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await JsonSerializer.SerializeAsync(fileStream, items, new JsonSerializerOptions { WriteIndented = true });
        }

        lock (_lock)
        {
            if (File.Exists(_filePath))
            {
                File.Replace(tempPath, _filePath, _filePath + ".bak");
            }
            else
            {
                File.Move(tempPath, _filePath);
            }
        }
    }

    public async Task<List<ItemPrice>> GetAllItemsAsync()
    {
        return await ReadFileAsync();
    }

    public async Task<ItemPrice?> GetItemByNameAsync(string name)
    {
        var items = await ReadFileAsync();
        return items.FirstOrDefault(i => i.Name.Equals(name, StringComparison.InvariantCultureIgnoreCase));
    }

    public async Task AddItemAsync(ItemPrice item)
    {
        var items = await ReadFileAsync();
        
        if (items.Any(i => i.Name.Equals(item.Name, StringComparison.InvariantCultureIgnoreCase)))
        {
            throw new Exception("El item ya existe.");
        }

        items.Add(item);
        await WriteFileAsync(items);
    }

    public async Task UpdateItemAsync(ItemPrice item)
    {
        var items = await ReadFileAsync();
        var existing = items.FirstOrDefault(i => i.Name.Equals(item.Name, StringComparison.InvariantCultureIgnoreCase));
        
        if (existing == null)
        {
            throw new Exception("El item no existe.");
        }

        existing.Price = item.Price;
        existing.Category = item.Category;
        existing.UpdatedAt = item.UpdatedAt;
        existing.UpdatedBy = item.UpdatedBy;

        await WriteFileAsync(items);
    }

    public async Task DeleteItemAsync(string name)
    {
        var items = await ReadFileAsync();
        var existing = items.FirstOrDefault(i => i.Name.Equals(name, StringComparison.InvariantCultureIgnoreCase));
        
        if (existing != null)
        {
            items.Remove(existing);
            await WriteFileAsync(items);
        }
    }

    public async Task ReplaceAllItemsAsync(List<ItemPrice> items)
    {
        await WriteFileAsync(items);
    }
}
