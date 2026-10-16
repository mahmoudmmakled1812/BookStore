using BookStore.Api.Domain;

namespace BookStore.Api.Services;

public static class QueryExtensions
{
    public static IQueryable<FlashSale> Active(this IQueryable<FlashSale> query, DateTime now) =>
        query.Where(f => f.StartsAt <= now && f.EndsAt >= now && f.Sold < f.StockLimit);
}