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
[Route("api/cart")]
public class CartController(AppDbContext db, IPricingService pricing) : ControllerBase
{
    private async Task<CartDto> ToDtoAsync(Cart cart) => pricing.ToDto(await pricing.PriceAsync(cart));

    [HttpGet]
    public async Task<ActionResult<CartDto>> Get()
    {
        var cart = await pricing.GetOrCreateCartAsync(User.GetUserId());
        return await ToDtoAsync(cart);
    }

    [HttpGet("count")]
    public async Task<ActionResult<object>> Count()
    {
        var uid = User.GetUserId();
        var count = await db.CartItems.Where(i => i.Cart.UserId == uid).SumAsync(i => (int?)i.Quantity) ?? 0;
        return new { count };
    }

    [HttpPost("items")]
    public async Task<ActionResult<CartDto>> AddItem(AddCartItemRequest request)
    {
        var book = await db.Books.FirstOrDefaultAsync(b => b.Id == request.BookId && b.IsActive);
        if (book is null) return NotFound();

        var cart = await pricing.GetOrCreateCartAsync(User.GetUserId());
        var item = cart.Items.FirstOrDefault(i => i.BookId == book.Id);
        var newQuantity = (item?.Quantity ?? 0) + request.Quantity;

        if (newQuantity > book.Stock)
            return Problem(detail: $"Only {book.Stock} copies are available.", statusCode: 400);

        if (item is null)
            cart.Items.Add(new CartItem { BookId = book.Id, Book = book, Quantity = newQuantity });
        else
            item.Quantity = newQuantity;

        cart.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return await ToDtoAsync(cart);
    }

    [HttpPut("items/{bookId:int}")]
    public async Task<ActionResult<CartDto>> UpdateItem(int bookId, UpdateCartItemRequest request)
    {
        var cart = await pricing.GetOrCreateCartAsync(User.GetUserId());
        var item = cart.Items.FirstOrDefault(i => i.BookId == bookId);
        if (item is null) return NotFound();

        if (request.Quantity > item.Book.Stock)
            return Problem(detail: $"Only {item.Book.Stock} copies are available.", statusCode: 400);

        item.Quantity = request.Quantity;
        cart.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return await ToDtoAsync(cart);
    }

    [HttpDelete("items/{bookId:int}")]
    public async Task<ActionResult<CartDto>> RemoveItem(int bookId)
    {
        var cart = await pricing.GetOrCreateCartAsync(User.GetUserId());
        var item = cart.Items.FirstOrDefault(i => i.BookId == bookId);
        if (item is not null)
        {
            cart.Items.Remove(item);
            db.CartItems.Remove(item);
            cart.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }

        return await ToDtoAsync(cart);
    }

    [HttpDelete]
    public async Task<ActionResult<CartDto>> Clear()
    {
        var cart = await pricing.GetOrCreateCartAsync(User.GetUserId());
        db.CartItems.RemoveRange(cart.Items.ToList());
        cart.Items.Clear();
        cart.DiscountCodeId = null;
        cart.DiscountCode = null;
        cart.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return await ToDtoAsync(cart);
    }

    [HttpPost("apply-discount")]
    public async Task<ActionResult<CartDto>> ApplyDiscount(ApplyDiscountRequest request)
    {
        var codeText = request.Code.Trim();
        var code = await db.DiscountCodes.FirstOrDefaultAsync(c => c.Code == codeText);
        if (code is null || !code.IsValidAt(DateTime.UtcNow))
            return Problem(detail: "This discount code is invalid or has expired.", statusCode: 400);

        var cart = await pricing.GetOrCreateCartAsync(User.GetUserId());
        if (code.BookId is not null && cart.Items.All(i => i.BookId != code.BookId))
            return Problem(detail: "This code applies to a specific book that is not in your cart.", statusCode: 400);

        cart.DiscountCodeId = code.Id;
        cart.DiscountCode = code;
        cart.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return await ToDtoAsync(cart);
    }

    [HttpDelete("discount")]
    public async Task<ActionResult<CartDto>> RemoveDiscount()
    {
        var cart = await pricing.GetOrCreateCartAsync(User.GetUserId());
        cart.DiscountCodeId = null;
        cart.DiscountCode = null;
        await db.SaveChangesAsync();
        return await ToDtoAsync(cart);
    }
}