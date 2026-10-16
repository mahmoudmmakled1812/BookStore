using BookStore.Api.Domain;
using BookStore.Api.Dtos;
using BookStore.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookStore.Api.Controllers;

[ApiController]
[Route("api/books")]
public class BooksController(ICatalogService catalog) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<BookDto>>> List([FromQuery] BookQuery q)
    {
        IQueryable<Book> query = catalog.ActiveBooks();

        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var s = q.Search.Trim();
            query = query.Where(b => b.Title.Contains(s) || b.Author.Contains(s));
        }
        if (q.CategoryId.HasValue) query = query.Where(b => b.CategoryId == q.CategoryId.Value);
        if (q.PublisherId.HasValue) query = query.Where(b => b.PublisherId == q.PublisherId.Value);
        if (q.Year.HasValue) query = query.Where(b => b.Year == q.Year.Value);
        if (q.MinPrice.HasValue) query = query.Where(b => b.Price >= q.MinPrice.Value);
        if (q.MaxPrice.HasValue) query = query.Where(b => b.Price <= q.MaxPrice.Value);

        var total = await query.CountAsync();

        IOrderedQueryable<Book> ordered = (q.Sort?.ToLowerInvariant()) switch
        {
            "price_asc" => query.OrderBy(b => b.Price),
            "price_desc" => query.OrderByDescending(b => b.Price),
            "oldest" => query.OrderBy(b => b.Year),
            "rating" => query.OrderByDescending(b => b.Reviews.Average(r => (double?)r.Rating)),
            _ => query.OrderByDescending(b => b.CreatedAt)
        };

        var (page, size) = Paging.Clamp(q.Page, q.PageSize);
        var items = await catalog.ToDtosAsync(ordered.ThenBy(b => b.Id).Skip((page - 1) * size).Take(size));

        return new PagedResult<BookDto>(items, page, size, total);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<BookDto>> GetById(int id)
    {
        var items = await catalog.ToDtosAsync(catalog.ActiveBooks().Where(b => b.Id == id));
        return items.Count == 0 ? NotFound() : items[0];
    }

    [HttpGet("best-sellers")]
    public async Task<ActionResult<List<BookDto>>> BestSellers([FromQuery] int count = 8)
    {
        count = Math.Clamp(count, 1, 50);
        var query = catalog.ActiveBooks()
            .OrderByDescending(b => b.IsBestSeller)
            .ThenByDescending(b => b.OrderItems.Sum(oi => (int?)oi.Quantity) ?? 0)
            .ThenBy(b => b.Id)
            .Take(count);
        return await catalog.ToDtosAsync(query);
    }

    [HttpGet("recommended")]
    public async Task<ActionResult<List<BookDto>>> Recommended([FromQuery] int count = 4)
    {
        count = Math.Clamp(count, 1, 50);
        var query = catalog.ActiveBooks()
            .OrderByDescending(b => b.Reviews.Average(r => (double?)r.Rating) ?? 0)
            .ThenByDescending(b => b.Reviews.Count())
            .ThenBy(b => b.Id)
            .Take(count);
        return await catalog.ToDtosAsync(query);
    }
}