using BookStore.Api.Data;
using BookStore.Api.Domain;
using BookStore.Api.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookStore.Api.Controllers;

[ApiController]
[Route("api/books/{bookId:int}/reviews")]
public class ReviewsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<ReviewDto>>> List(int bookId, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        var query = db.Reviews.AsNoTracking().Where(r => r.BookId == bookId);
        var total = await query.CountAsync();
        var (p, size) = Paging.Clamp(page, pageSize);

        var items = await query
            .OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id)
            .Skip((p - 1) * size).Take(size)
            .Select(r => new ReviewDto(r.Id, r.User.FullName, r.User.ProfileImageUrl, r.Rating, r.Comment, r.CreatedAt))
            .ToListAsync();

        return new PagedResult<ReviewDto>(items, p, size, total);
    }

    /// <summary>Creates the user's review for this book, or updates it if one already exists.</summary>
    [Authorize]
    [HttpPost]
    public async Task<ActionResult<ReviewDto>> Upsert(int bookId, ReviewRequest request)
    {
        if (!await db.Books.AnyAsync(b => b.Id == bookId && b.IsActive)) return NotFound();

        var uid = User.GetUserId();
        var review = await db.Reviews.FirstOrDefaultAsync(r => r.BookId == bookId && r.UserId == uid);
        if (review is null)
        {
            review = new Review { BookId = bookId, UserId = uid };
            db.Reviews.Add(review);
        }

        review.Rating = request.Rating;
        review.Comment = request.Comment?.Trim();
        review.CreatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        var user = await db.Users.AsNoTracking().FirstAsync(u => u.Id == uid);
        return new ReviewDto(review.Id, user.FullName, user.ProfileImageUrl, review.Rating, review.Comment, review.CreatedAt);
    }
}