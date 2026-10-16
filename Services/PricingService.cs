using BookStore.Api.Data;
using BookStore.Api.Domain;
using BookStore.Api.Dtos;
using BookStore.Api.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BookStore.Api.Services;

public class PricedLine
{
    public Book Book { get; init; } = null!;
    public int Quantity { get; init; }
    public decimal UnitPrice { get; init; }
    public decimal OriginalPrice { get; init; }
    public FlashSale? Flash { get; init; }
    public decimal LineTotal => UnitPrice * Quantity;
}

public class PricedCart
{
    public List<PricedLine> Lines { get; init; } = new();
    public decimal Subtotal { get; set; }
    public decimal Discount { get; set; }
    public decimal Tax { get; set; }
    public decimal Shipping { get; set; }
    public decimal Total { get; set; }
    public string? DiscountCode { get; set; }
    public int ItemCount => Lines.Sum(l => l.Quantity);
}

public interface IPricingService
{
    /// <summary>Loads the user's cart (with books and discount code), creating it when missing.</summary>
    Task<Cart> GetOrCreateCartAsync(string userId);

    Task<PricedCart> PriceAsync(Cart cart);

    CartDto ToDto(PricedCart priced);
}

public class PricingService(AppDbContext db, IOptions<PricingOptions> options) : IPricingService
{
    private static decimal Round(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);

    public async Task<Cart> GetOrCreateCartAsync(string userId)
    {
        var cart = await db.Carts
            .Include(c => c.Items).ThenInclude(i => i.Book)
            .Include(c => c.DiscountCode)
            .FirstOrDefaultAsync(c => c.UserId == userId);

        if (cart is not null) return cart;

        cart = new Cart { UserId = userId };
        db.Carts.Add(cart);
        await db.SaveChangesAsync();
        return cart;
    }

    public async Task<PricedCart> PriceAsync(Cart cart)
    {
        var now = DateTime.UtcNow;
        var bookIds = cart.Items.Select(i => i.BookId).ToList();
        var flashes = await db.FlashSales.Active(now).Where(f => bookIds.Contains(f.BookId)).ToListAsync();

        var priced = new PricedCart();

        foreach (var item in cart.Items)
        {
            // Flash price applies to the whole line only when enough sale copies are left.
            var flash = flashes
                .Where(f => f.BookId == item.BookId && f.StockLimit - f.Sold >= item.Quantity)
                .OrderBy(f => f.SalePrice)
                .FirstOrDefault();

            priced.Lines.Add(new PricedLine
            {
                Book = item.Book,
                Quantity = item.Quantity,
                OriginalPrice = item.Book.Price,
                UnitPrice = flash?.SalePrice ?? item.Book.Price,
                Flash = flash
            });
        }

        priced.Subtotal = Round(priced.Lines.Sum(l => l.LineTotal));

        var code = cart.DiscountCode;
        if (code is not null && code.IsValidAt(now))
        {
            var basis = code.BookId is null
                ? priced.Subtotal
                : priced.Lines.Where(l => l.Book.Id == code.BookId).Sum(l => l.LineTotal);

            priced.Discount = Round(basis * code.Percent / 100m);
            priced.DiscountCode = priced.Discount > 0 ? code.Code : null;
        }

        var taxable = priced.Subtotal - priced.Discount;
        priced.Tax = Round(taxable * options.Value.TaxRate);
        priced.Shipping = priced.Lines.Count == 0 ? 0 : options.Value.ShippingFee;
        priced.Total = taxable + priced.Tax + priced.Shipping;
        return priced;
    }

    public CartDto ToDto(PricedCart p) => new(
        p.Lines.Select(l => new CartLineDto(
            l.Book.Id, l.Book.Title, l.Book.Author, l.Book.ImageUrl, l.Book.Asin,
            l.UnitPrice, l.OriginalPrice, l.Flash is not null,
            l.Quantity, l.LineTotal, l.Book.Stock)).ToList(),
        p.ItemCount, p.Subtotal, p.Discount, p.DiscountCode, p.Tax, p.Shipping, p.Total);
}