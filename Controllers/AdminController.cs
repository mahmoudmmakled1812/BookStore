using BookStore.Api.Data;
using BookStore.Api.Domain;
using BookStore.Api.Dtos;
using BookStore.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookStore.Api.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin")]
public class AdminController(AppDbContext db, ICatalogService catalog, IOrderService orders) : ControllerBase
{
    // ───── Books ─────

    /// <summary>All books including hidden (soft-deleted) ones. The public API only shows active books.</summary>
    [HttpGet("books")]
    public async Task<ActionResult<PagedResult<BookDto>>> ListBooks([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var query = db.Books.AsNoTracking();
        var total = await query.CountAsync();
        var (p, size) = Paging.Clamp(page, pageSize);
        var items = await catalog.ToDtosAsync(query.OrderByDescending(b => b.CreatedAt).ThenBy(b => b.Id).Skip((p - 1) * size).Take(size));
        return new PagedResult<BookDto>(items, p, size, total);
    }

    [HttpPost("books")]
    public async Task<IActionResult> CreateBook(UpsertBookRequest request)
    {
        if (!await RefsExistAsync(request)) return Problem(detail: "Category or publisher does not exist.", statusCode: 400);

        var book = new Book();
        Apply(book, request);
        db.Books.Add(book);
        await db.SaveChangesAsync();
        return Created($"/api/books/{book.Id}", new { book.Id });
    }

    [HttpPut("books/{id:int}")]
    public async Task<IActionResult> UpdateBook(int id, UpsertBookRequest request)
    {
        var book = await db.Books.FirstOrDefaultAsync(b => b.Id == id);
        if (book is null) return NotFound();
        if (!await RefsExistAsync(request)) return Problem(detail: "Category or publisher does not exist.", statusCode: 400);

        Apply(book, request);
        book.IsActive = true;
        await db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Soft delete: the book is hidden from the store but order history stays intact.</summary>
    [HttpDelete("books/{id:int}")]
    public async Task<IActionResult> DeleteBook(int id)
    {
        var book = await db.Books.FirstOrDefaultAsync(b => b.Id == id);
        if (book is null) return NotFound();
        book.IsActive = false;
        await db.SaveChangesAsync();
        return NoContent();
    }

    private async Task<bool> RefsExistAsync(UpsertBookRequest r) =>
        await db.Categories.AnyAsync(c => c.Id == r.CategoryId) &&
        await db.Publishers.AnyAsync(p => p.Id == r.PublisherId);

    private static void Apply(Book book, UpsertBookRequest r)
    {
        book.Title = r.Title.Trim();
        book.Author = r.Author.Trim();
        book.Description = r.Description.Trim();
        book.ImageUrl = r.ImageUrl.Trim();
        book.Price = r.Price;
        book.Year = r.Year;
        book.Asin = r.Asin;
        book.Stock = r.Stock;
        book.IsBestSeller = r.IsBestSeller;
        book.CategoryId = r.CategoryId;
        book.PublisherId = r.PublisherId;
    }

    // ───── Categories & publishers ─────

    [HttpPost("categories")]
    public async Task<IActionResult> CreateCategory(CreateNamedRequest request)
    {
        var name = request.Name.Trim();
        if (await db.Categories.AnyAsync(c => c.Name == name)) return Conflict(new ProblemDetails { Title = "Category already exists.", Status = 409 });
        var c = new Category { Name = name };
        db.Categories.Add(c);
        await db.SaveChangesAsync();
        return Ok(new { c.Id, c.Name });
    }

    [HttpPost("publishers")]
    public async Task<IActionResult> CreatePublisher(CreateNamedRequest request)
    {
        var name = request.Name.Trim();
        if (await db.Publishers.AnyAsync(p => p.Name == name)) return Conflict(new ProblemDetails { Title = "Publisher already exists.", Status = 409 });
        var p = new Publisher { Name = name };
        db.Publishers.Add(p);
        await db.SaveChangesAsync();
        return Ok(new { p.Id, p.Name });
    }

    // ───── Flash sales & discount codes ─────

    [HttpPost("flash-sales")]
    public async Task<IActionResult> CreateFlashSale(CreateFlashSaleRequest request)
    {
        var book = await db.Books.FirstOrDefaultAsync(b => b.Id == request.BookId && b.IsActive);
        if (book is null) return Problem(detail: "Book not found.", statusCode: 400);
        if (request.SalePrice >= book.Price) return Problem(detail: "Sale price must be lower than the book price.", statusCode: 400);
        if (request.EndsAt <= request.StartsAt) return Problem(detail: "EndsAt must be after StartsAt.", statusCode: 400);

        var sale = new FlashSale
        {
            BookId = book.Id,
            SalePrice = request.SalePrice,
            StartsAt = request.StartsAt.ToUniversalTime(),
            EndsAt = request.EndsAt.ToUniversalTime(),
            StockLimit = request.StockLimit
        };
        db.FlashSales.Add(sale);
        await db.SaveChangesAsync();
        return Ok(new { sale.Id });
    }

    [HttpGet("discount-codes")]
    public async Task<ActionResult<List<DiscountCode>>> ListDiscountCodes() =>
        await db.DiscountCodes.AsNoTracking().OrderBy(c => c.Code).ToListAsync();

    [HttpPost("discount-codes")]
    public async Task<IActionResult> CreateDiscountCode(CreateDiscountCodeRequest request)
    {
        var code = request.Code.Trim();
        if (await db.DiscountCodes.AnyAsync(c => c.Code == code)) return Conflict(new ProblemDetails { Title = "Code already exists.", Status = 409 });
        if (request.BookId is not null && !await db.Books.AnyAsync(b => b.Id == request.BookId))
            return Problem(detail: "Book not found.", statusCode: 400);

        var entity = new DiscountCode { Code = code, Percent = request.Percent, BookId = request.BookId, ExpiresAt = request.ExpiresAt?.ToUniversalTime() };
        db.DiscountCodes.Add(entity);
        await db.SaveChangesAsync();
        return Ok(new { entity.Id });
    }

    [HttpPut("discount-codes/{id:int}/active")]
    public async Task<IActionResult> SetDiscountActive(int id, [FromQuery] bool value)
    {
        var code = await db.DiscountCodes.FirstOrDefaultAsync(c => c.Id == id);
        if (code is null) return NotFound();
        code.IsActive = value;
        await db.SaveChangesAsync();
        return NoContent();
    }

    // ───── Orders ─────

    [HttpGet("orders")]
    public async Task<ActionResult<PagedResult<OrderSummaryDto>>> ListOrders(
        [FromQuery] OrderStatus? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var query = db.Orders.AsNoTracking().AsQueryable();
        if (status.HasValue) query = query.Where(o => o.Status == status.Value);

        var total = await query.CountAsync();
        var (p, size) = Paging.Clamp(page, pageSize);
        var items = await query
            .OrderByDescending(o => o.CreatedAt).ThenByDescending(o => o.Id)
            .Skip((p - 1) * size).Take(size)
            .Select(o => new OrderSummaryDto(o.Id, o.OrderNumber, o.Status, o.CreatedAt, o.ShippingAddress, o.Total, o.Items.Sum(i => i.Quantity)))
            .ToListAsync();

        return new PagedResult<OrderSummaryDto>(items, p, size, total);
    }

    [HttpGet("orders/{id:int}")]
    public async Task<ActionResult<OrderDetailDto>> GetOrder(int id)
    {
        var order = await db.Orders.AsNoTracking().Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == id);
        return order is null ? NotFound() : OrderDetailDto.From(order);
    }

    [HttpPut("orders/{id:int}/status")]
    public async Task<IActionResult> UpdateOrderStatus(int id, UpdateOrderStatusRequest request)
    {
        var order = await db.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == id);
        if (order is null) return NotFound();

        if (order.Status != OrderStatus.InProgress)
            return Problem(detail: "Only orders that are in progress can change status.", statusCode: 400);

        switch (request.Status)
        {
            case OrderStatus.Canceled:
                await orders.CancelAsync(order);
                break;

            case OrderStatus.Completed:
                order.Status = OrderStatus.Completed;
                await db.SaveChangesAsync();
                break;

            default:
                return Problem(detail: "Status must be Completed or Canceled.", statusCode: 400);
        }

        return NoContent();
    }

    // ───── Contact messages ─────

    [HttpGet("contact-messages")]
    public async Task<ActionResult<List<ContactMessage>>> ContactMessages() =>
        await db.ContactMessages.AsNoTracking().OrderByDescending(m => m.CreatedAt).Take(200).ToListAsync();
}