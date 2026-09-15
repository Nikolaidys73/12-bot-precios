using System.Collections.Generic;
using System.Threading.Tasks;
using L2PriceBot.App.Models;

namespace L2PriceBot.App.Repositories;

public interface IPriceRepository
{
    Task<List<ItemPrice>> GetAllItemsAsync();
    Task<ItemPrice?> GetItemByNameAsync(string name);
    Task AddItemAsync(ItemPrice item);
    Task UpdateItemAsync(ItemPrice item);
    Task DeleteItemAsync(string name);
    Task ReplaceAllItemsAsync(List<ItemPrice> items);
}
