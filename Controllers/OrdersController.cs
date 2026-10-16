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
[Route("api/orders")]
public class OrdersController(AppDbContext db, IPricingService pricing, IOrderService orders) : ControllerBase
{
    /// <summary>Creates an order from the current cart (payment: cash on delivery).</summary>
    [HttpPost]
    public async Task<ActionResult<OrderDetailDto>> Checkout(CheckoutRequest request)
    {
        var uid = User.GetUserId();
        var cart = await pricing.GetOrCreateCartAsync(uid);

        if (cart.Items.Count == 0)
            return Problem(detail: "Your cart is empty.", statusCode: 400);

        foreach (var item in cart.Items)
        {
            if (!item.Book.IsActive || item.Quantity > item.Book.Stock)
                return Problem(detail: $"\"{item.Book.Title}\" does not have enough stock ({item.Book.Stock} left).", statusCode: 400);
        }

        var priced = await pricing.PriceAsync(cart);

        await using var tx = await db.Database.BeginTransactionAsync();
        try
        {
            var order = new Order
            {
                OrderNumber = Guid.NewGuid().ToString("N"), // replaced by a short number right after the insert
                UserId = uid,
                ShippingAddress = request.ShippingAddress.Trim(),
                PhoneNumber = request.PhoneNumber.Trim(),
                PaymentMethod = "CashOnDelivery",
                DiscountCode = priced.DiscountCode,
                Subtotal = priced.Subtotal,
                Discount = priced.Discount,
                Tax = priced.Tax,
                Shipping = priced.Shipping,
                Total = priced.Total,
                Items = priced.Lines.Select(l => new OrderItem
                {
                    BookId = l.Book.Id,
                    Title = l.Book.Title,
                    Author = l.Book.Author,
                    ImageUrl = l.Book.ImageUrl,
                    UnitPrice = l.UnitPrice,
                    Quantity = l.Quantity
                }).ToList()
            };

            foreach (var line in priced.Lines)
            {
                line.Book.Stock -= line.Quantity;
                if (line.Flash is not null) line.Flash.Sold += line.Quantity;
            }

            db.Orders.Add(order);
            db.CartItems.RemoveRange(cart.Items.ToList());
            cart.Items.Clear();
            cart.DiscountCodeId = null;
            cart.DiscountCode = null;
            cart.UpdatedAt = DateTime.UtcNow;

            await db.SaveChangesAsync();

            order.OrderNumber = (100000 + order.Id).ToString();
            await db.SaveChangesAsync();
            await tx.CommitAsync();

            return CreatedAtAction(nameof(Get), new { id = order.Id }, OrderDetailDto.From(order));
        }
        catch (DbUpdateConcurrencyException)
        {
            return Problem(detail: "Stock changed while placing your order. Please review your cart and try again.", statusCode: 409);
        }
    }

    /// <summary>Order history. Optional status filter: InProgress | Completed | Canceled.</summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<OrderSummaryDto>>> List(
        [FromQuery] OrderStatus? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        var uid = User.GetUserId();
        var query = db.Orders.AsNoTracking().Where(o => o.UserId == uid);
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

    [HttpGet("{id:int}")]
    public async Task<ActionResult<OrderDetailDto>> Get(int id)
    {
        var uid = User.GetUserId();
        var order = await db.Orders.AsNoTracking().Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == id && o.UserId == uid);
        return order is null ? NotFound() : OrderDetailDto.From(order);
    }

    [HttpPost("{id:int}/cancel")]
    public async Task<ActionResult<OrderDetailDto>> Cancel(int id)
    {
        var uid = User.GetUserId();
        var order = await db.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == id && o.UserId == uid);
        if (order is null) return NotFound();

        if (order.Status != OrderStatus.InProgress)
            return Problem(detail: "Only orders that are in progress can be canceled.", statusCode: 400);

        await orders.CancelAsync(order);
        return OrderDetailDto.From(order);
    }
}