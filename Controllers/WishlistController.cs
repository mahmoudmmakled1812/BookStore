using BookStore.Api.Data;
using BookStore.Api.Domain;
using BookStore.Api.Dtos;
using BookStore.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookStore.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/wishlist")]
public class WishlistController(AppDbContext db, ICatalogService catalog) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<BookDto>>> Get()
    {
        var uid = User.GetUserId();
        var ids = db.WishlistItems.Where(w => w.UserId == uid).Select(w => w.BookId);
        return await catalog.ToDtosAsync(catalog.ActiveBooks().Where(b => ids.Contains(b.Id)).OrderBy(b => b.Title));
    }

    [HttpGet("count")]
    public async Task<ActionResult<object>> Count()
    {
        var uid = User.GetUserId();
        return new { count = await db.WishlistItems.CountAsync(w => w.UserId == uid) };
    }

    [HttpPost("{bookId:int}")]
    public async Task<ActionResult<object>> Add(int bookId)
    {
        var uid = User.GetUserId();
        if (!await db.Books.AnyAsync(b => b.Id == bookId && b.IsActive)) return NotFound();

        if (!await db.WishlistItems.AnyAsync(w => w.UserId == uid && w.BookId == bookId))
        {
            db.WishlistItems.Add(new WishlistItem { UserId = uid, BookId = bookId });
            await db.SaveChangesAsync();
        }

        return new { count = await db.WishlistItems.CountAsync(w => w.UserId == uid) };
    }

    [HttpDelete("{bookId:int}")]
    public async Task<ActionResult<object>> Remove(int bookId)
    {
        var uid = User.GetUserId();
        var item = await db.WishlistItems.FirstOrDefaultAsync(w => w.UserId == uid && w.BookId == bookId);
        if (item is not null)
        {
            db.WishlistItems.Remove(item);
            await db.SaveChangesAsync();
        }

        return new { count = await db.WishlistItems.CountAsync(w => w.UserId == uid) };
    }
}