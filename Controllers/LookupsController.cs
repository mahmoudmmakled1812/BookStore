using BookStore.Api.Data;
using BookStore.Api.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookStore.Api.Controllers;

/// <summary>Filter data for the Library page sidebar.</summary>
[ApiController]
[Route("api")]
public class LookupsController(AppDbContext db) : ControllerBase
{
    [HttpGet("categories")]
    public async Task<ActionResult<LookupListDto>> Categories()
    {
        var items = await db.Categories.AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => new LookupDto(c.Id, c.Name, c.Books.Count(b => b.IsActive)))
            .ToListAsync();
        return new LookupListDto(items.Sum(i => i.BooksCount), items);
    }

    [HttpGet("publishers")]
    public async Task<ActionResult<LookupListDto>> Publishers()
    {
        var items = await db.Publishers.AsNoTracking()
            .OrderBy(p => p.Name)
            .Select(p => new LookupDto(p.Id, p.Name, p.Books.Count(b => b.IsActive)))
            .ToListAsync();
        return new LookupListDto(items.Sum(i => i.BooksCount), items);
    }

    [HttpGet("years")]
    public async Task<ActionResult<List<int>>> Years() =>
        await db.Books.AsNoTracking().Where(b => b.IsActive)
            .Select(b => b.Year).Distinct().OrderByDescending(y => y).ToListAsync();
}