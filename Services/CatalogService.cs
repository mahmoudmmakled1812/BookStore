using BookStore.Api.Data;
using BookStore.Api.Domain;
using BookStore.Api.Dtos;
using Microsoft.EntityFrameworkCore;

namespace BookStore.Api.Services;

public interface ICatalogService
{
    IQueryable<Book> ActiveBooks();

    Task<List<BookDto>> ToDtosAsync(IQueryable<Book> query);

    Task<Dictionary<int, (double Average, int Count)>> GetRatingsAsync(IEnumerable<int> bookIds);
}

public class CatalogService(AppDbContext db) : ICatalogService
{
    public IQueryable<Book> ActiveBooks() => db.Books.AsNoTracking().Where(b => b.IsActive);

    public async Task<List<BookDto>> ToDtosAsync(IQueryable<Book> query)
    {
        var rows = await query.Select(b => new
        {
            b.Id,
            b.Title,
            b.Author,
            b.Description,
            b.ImageUrl,
            b.Price,
            b.Year,
            b.Stock,
            Category = b.Category.Name,
            Publisher = b.Publisher.Name,
            ReviewsCount = b.Reviews.Count(),
            Rating = b.Reviews.Select(r => (double?)r.Rating).Average() ?? 0
        }).ToListAsync();

        var ids = rows.Select(r => r.Id).ToList();
        var now = DateTime.UtcNow;

        var flashes = await db.FlashSales.AsNoTracking().Active(now)
            .Where(f => ids.Contains(f.BookId)).ToListAsync();
        var flashByBook = flashes.GroupBy(f => f.BookId)
            .ToDictionary(g => g.Key, g => g.OrderBy(f => f.SalePrice).First());

        var codes = await db.DiscountCodes.AsNoTracking()
            .Where(c => c.IsActive && c.BookId != null && ids.Contains(c.BookId.Value)
                        && (c.ExpiresAt == null || c.ExpiresAt > now))
            .ToListAsync();
        var codeByBook = codes.GroupBy(c => c.BookId!.Value)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(c => c.Percent).First());

        return rows.Select(r =>
        {
            flashByBook.TryGetValue(r.Id, out var flash);
            codeByBook.TryGetValue(r.Id, out var code);
            return new BookDto(
                r.Id, r.Title, r.Author, r.Description, r.ImageUrl,
                r.Price, flash?.SalePrice ?? r.Price, r.Year, r.Category, r.Publisher,
                Math.Round(r.Rating, 1), r.ReviewsCount, r.Stock, r.Stock > 0,
                code is null ? null : new BookDiscountDto(code.Code, code.Percent),
                flash is null ? null : new BookFlashSaleDto(flash.Id, flash.SalePrice, flash.EndsAt, flash.StockLimit - flash.Sold));
        }).ToList();
    }

    public async Task<Dictionary<int, (double Average, int Count)>> GetRatingsAsync(IEnumerable<int> bookIds)
    {
        var ids = bookIds.Distinct().ToList();
        var rows = await db.Reviews.AsNoTracking()
            .Where(r => ids.Contains(r.BookId))
            .GroupBy(r => r.BookId)
            .Select(g => new { BookId = g.Key, Avg = g.Average(r => (double)r.Rating), Count = g.Count() })
            .ToListAsync();

        return rows.ToDictionary(r => r.BookId, r => (Math.Round(r.Avg, 1), r.Count));
    }
}