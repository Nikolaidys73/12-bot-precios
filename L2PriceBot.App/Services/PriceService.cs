using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using L2PriceBot.App.Models;
using L2PriceBot.App.Repositories;

namespace L2PriceBot.App.Services;

public class PriceService
{
    private readonly IPriceRepository _priceRepository;

    public PriceService(IPriceRepository priceRepository)
    {
        _priceRepository = priceRepository;
    }

    public async Task<List<ItemPrice>> GetAllItemsAsync()
    {
        var items = await _priceRepository.GetAllItemsAsync();
        return items.OrderBy(i => i.Category).ThenBy(i => i.Name).ToList();
    }
    
    public async Task<List<ItemPrice>> SearchItemsAsync(string query)
    {
        var items = await _priceRepository.GetAllItemsAsync();
        return items.Where(i => i.Name.Contains(query, StringComparison.InvariantCultureIgnoreCase))
                    .OrderBy(i => i.Name)
                    .ToList();
    }

    public async Task<ItemPrice?> GetItemByNameAsync(string name)
    {
        return await _priceRepository.GetItemByNameAsync(name);
    }

    public async Task<ItemPrice> AddItemAsync(string name, int price, string category, string updatedBy)
    {
        if (price <= 0)
        {
            throw new ArgumentException("El precio debe ser mayor a cero.");
        }

        var item = new ItemPrice
        {
            Name = name.Trim(),
            Price = price,
            Category = string.IsNullOrWhiteSpace(category) ? "General" : category.Trim().ToUpper(),
            UpdatedAt = DateTime.UtcNow,
            UpdatedBy = updatedBy
        };

        await _priceRepository.AddItemAsync(item);
        return item;
    }

    public async Task<ItemPrice> UpdateItemAsync(string name, int price, string updatedBy)
    {
        if (price <= 0)
        {
            throw new ArgumentException("El precio debe ser mayor a cero.");
        }

        var existing = await _priceRepository.GetItemByNameAsync(name);
        if (existing == null)
        {
            throw new ArgumentException("No encontré ese item.");
        }

        existing.Price = price;
        existing.UpdatedAt = DateTime.UtcNow;
        existing.UpdatedBy = updatedBy;

        await _priceRepository.UpdateItemAsync(existing);
        return existing;
    }

    public async Task DeleteItemAsync(string name)
    {
        var existing = await _priceRepository.GetItemByNameAsync(name);
        if (existing == null)
        {
            throw new ArgumentException("No encontré ese item.");
        }
        await _priceRepository.DeleteItemAsync(name);
    }

    public async Task ReplaceAllItemsAsync(List<ItemPrice> items)
    {
        await _priceRepository.ReplaceAllItemsAsync(items);
    }
}
