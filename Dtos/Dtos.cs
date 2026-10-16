using BookStore.Api.Domain;
using System.ComponentModel.DataAnnotations;

namespace BookStore.Api.Dtos;

public class RegisterRequest
{
    [Required, StringLength(100)] public string FullName { get; set; } = "";
    [Required, EmailAddress] public string Email { get; set; } = "";
    [Required, MinLength(6)] public string Password { get; set; } = "";
    [Required, Compare(nameof(Password))] public string ConfirmPassword { get; set; } = "";
    [Phone] public string? PhoneNumber { get; set; }
}

public class LoginRequest
{
    [Required, EmailAddress] public string Email { get; set; } = "";
    [Required] public string Password { get; set; } = "";
}

public class UpdateProfileRequest
{
    [Required, StringLength(100)] public string FullName { get; set; } = "";
    [Required, EmailAddress] public string Email { get; set; } = "";
    [Phone] public string? PhoneNumber { get; set; }
    [StringLength(300)] public string? Address { get; set; }
    [StringLength(500)] public string? ProfileImageUrl { get; set; }
}

public class ChangePasswordRequest
{
    [Required] public string CurrentPassword { get; set; } = "";
    [Required, MinLength(6)] public string NewPassword { get; set; } = "";
}

public class BookQuery
{
    public string? Search { get; set; }
    public int? CategoryId { get; set; }
    public int? PublisherId { get; set; }
    public int? Year { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }

    public string? Sort { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 12;
}

public class ReviewRequest
{
    [Range(1, 5)] public int Rating { get; set; }
    [StringLength(1000)] public string? Comment { get; set; }
}

public class AddCartItemRequest
{
    [Required] public int BookId { get; set; }
    [Range(1, 100)] public int Quantity { get; set; } = 1;
}

public class UpdateCartItemRequest
{
    [Range(1, 100)] public int Quantity { get; set; }
}

public class ApplyDiscountRequest
{
    [Required, StringLength(50)] public string Code { get; set; } = "";
}

public class CheckoutRequest
{
    [Required, StringLength(300)] public string ShippingAddress { get; set; } = "";
    [Required, Phone] public string PhoneNumber { get; set; } = "";
}

public class ContactRequest
{
    [Required, StringLength(100)] public string Name { get; set; } = "";
    [Required, EmailAddress] public string Email { get; set; } = "";
    [StringLength(150)] public string? Subject { get; set; }
    [Required, StringLength(2000)] public string Message { get; set; } = "";
}

public class UpsertBookRequest
{
    [Required, StringLength(200)] public string Title { get; set; } = "";
    [Required, StringLength(150)] public string Author { get; set; } = "";
    [Required] public string Description { get; set; } = "";
    [Required] public string ImageUrl { get; set; } = "";
    [Range(0.01, 100000)] public decimal Price { get; set; }
    [Range(1000, 2100)] public int Year { get; set; }
    public string? Asin { get; set; }
    [Range(0, 1000000)] public int Stock { get; set; }
    public bool IsBestSeller { get; set; }
    public int CategoryId { get; set; }
    public int PublisherId { get; set; }
}

public class CreateNamedRequest
{
    [Required, StringLength(100)] public string Name { get; set; } = "";
}

public class CreateFlashSaleRequest
{
    public int BookId { get; set; }
    [Range(0.01, 100000)] public decimal SalePrice { get; set; }
    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }
    [Range(1, 100000)] public int StockLimit { get; set; }
}

public class CreateDiscountCodeRequest
{
    [Required, StringLength(50)] public string Code { get; set; } = "";
    [Range(1, 90)] public int Percent { get; set; }
    public int? BookId { get; set; }
    public DateTime? ExpiresAt { get; set; }
}

public class UpdateOrderStatusRequest
{
    public OrderStatus Status { get; set; }
}

public record UserDto(string Id, string FullName, string Email, string? PhoneNumber, string? Address, string? ProfileImageUrl, IList<string> Roles)
{
    public static UserDto From(ApplicationUser u, IList<string> roles) =>
        new(u.Id, u.FullName, u.Email ?? "", u.PhoneNumber, u.Address, u.ProfileImageUrl, roles);
}

public record AuthResponse(string Token, DateTime ExpiresAt, UserDto User);

public record PagedResult<T>(List<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}

public static class Paging
{
    public static (int Page, int Size) Clamp(int page, int size) => (Math.Max(1, page), Math.Clamp(size, 1, 50));
}

public record BookDiscountDto(string Code, int Percent);
public record BookFlashSaleDto(int Id, decimal SalePrice, DateTime EndsAt, int Remaining);

public record BookDto(
    int Id, string Title, string Author, string Description, string ImageUrl,
    decimal Price, decimal CurrentPrice, int Year, string Category, string Publisher,
    double Rating, int ReviewsCount, int Stock, bool InStock,
    BookDiscountDto? Discount, BookFlashSaleDto? FlashSale);

public record LookupDto(int Id, string Name, int BooksCount);
public record LookupListDto(int TotalBooks, List<LookupDto> Items);

public record FlashSaleDto(
    int Id, int BookId, string Title, string Author, string ImageUrl,
    decimal OriginalPrice, decimal SalePrice, double Rating, int ReviewsCount,
    int Remaining, DateTime StartsAt, DateTime EndsAt, int SecondsLeft);

public record ReviewDto(int Id, string UserName, string? UserImageUrl, int Rating, string? Comment, DateTime CreatedAt);

public record CartLineDto(
    int BookId, string Title, string Author, string ImageUrl, string? Asin,
    decimal UnitPrice, decimal OriginalPrice, bool OnFlashSale,
    int Quantity, decimal LineTotal, int Stock);

public record CartDto(
    List<CartLineDto> Items, int ItemCount, decimal Subtotal, decimal Discount,
    string? DiscountCode, decimal Tax, decimal Shipping, decimal Total);

public record OrderItemDto(int BookId, string Title, string Author, string ImageUrl, decimal UnitPrice, int Quantity, decimal LineTotal);

public record OrderSummaryDto(int Id, string OrderNumber, OrderStatus Status, DateTime CreatedAt, string ShippingAddress, decimal Total, int ItemsCount);

public record OrderDetailDto(
    int Id, string OrderNumber, OrderStatus Status, DateTime CreatedAt,
    string ShippingAddress, string PhoneNumber, string PaymentMethod,
    decimal Subtotal, decimal Discount, string? DiscountCode, decimal Tax, decimal Shipping, decimal Total,
    List<OrderItemDto> Items)
{
    public static OrderDetailDto From(Order o) => new(
        o.Id, o.OrderNumber, o.Status, o.CreatedAt, o.ShippingAddress, o.PhoneNumber, o.PaymentMethod,
        o.Subtotal, o.Discount, o.DiscountCode, o.Tax, o.Shipping, o.Total,
        o.Items.Select(i => new OrderItemDto(i.BookId, i.Title, i.Author, i.ImageUrl, i.UnitPrice, i.Quantity, i.UnitPrice * i.Quantity)).ToList());
}