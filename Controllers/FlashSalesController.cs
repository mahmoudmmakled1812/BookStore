using BookStore.Api.Data;
using BookStore.Api.Dtos;
using BookStore.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookStore.Api.Controllers;

[ApiController]
[Route("api/flash-sales")]
public class FlashSalesController(AppDbContext db, ICatalogService catalog) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<FlashSaleDto>>> GetActive()
    {
        var now = DateTime.UtcNow;
        var sales = await db.FlashSales.AsNoTracking().Active(now)
            .Include(f => f.Book)
            .Where(f => f.Book.IsActive)
            .OrderBy(f => f.EndsAt)
            .ToListAsync();

        var ratings = await catalog.GetRatingsAsync(sales.Select(s => s.BookId));

        return sales.Select(s =>
        {
            ratings.TryGetValue(s.BookId, out var r);
            return new FlashSaleDto(
                s.Id, s.BookId, s.Book.Title, s.Book.Author, s.Book.ImageUrl,
                s.Book.Price, s.SalePrice, r.Average, r.Count,
                s.StockLimit - s.Sold, s.StartsAt, s.EndsAt,
                (int)Math.Max(0, (s.EndsAt - now).TotalSeconds));
        }).ToList();
    }
}