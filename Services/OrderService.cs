using BookStore.Api.Data;
using BookStore.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace BookStore.Api.Services;

public interface IOrderService
{
    Task CancelAsync(Order order);
}

public class OrderService(AppDbContext db) : IOrderService
{
    public async Task CancelAsync(Order order)
    {
        var ids = order.Items.Select(i => i.BookId).ToList();
        var books = await db.Books.Where(b => ids.Contains(b.Id)).ToListAsync();

        foreach (var item in order.Items)
            books.First(b => b.Id == item.BookId).Stock += item.Quantity;

        order.Status = OrderStatus.Canceled;
        await db.SaveChangesAsync();
    }
}