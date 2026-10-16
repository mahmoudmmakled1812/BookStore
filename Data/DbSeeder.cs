using BookStore.Api.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BookStore.Api.Data;

public static class DbSeeder
{
    private const string Img = "https://book-store-chi-topaz.vercel.app/images/";

    public static async Task SeedAsync(IServiceProvider services, IConfiguration config)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        await db.Database.EnsureCreatedAsync();

        foreach (var role in new[] { "Admin", "Customer" })
            if (!await roles.RoleExistsAsync(role))
                await roles.CreateAsync(new IdentityRole(role));

        var adminEmail = config["Seed:AdminEmail"] ?? "admin@bookstore.com";
        var adminPassword = config["Seed:AdminPassword"] ?? "Admin@12345";
        if (await users.FindByEmailAsync(adminEmail) is null)
        {
            var admin = new ApplicationUser { UserName = adminEmail, Email = adminEmail, FullName = "Store Admin", EmailConfirmed = true };
            var result = await users.CreateAsync(admin, adminPassword);
            if (result.Succeeded) await users.AddToRoleAsync(admin, "Admin");
        }

        if (await db.Books.AnyAsync()) return;

        var business = new Category { Name = "Business" };
        var selfHelp = new Category { Name = "Self Help" };
        var fantasy = new Category { Name = "Fantasy" };
        var history = new Category { Name = "History" };
        var kids = new Category { Name = "Kids" };
        var art = new Category { Name = "Art" };
        db.Categories.AddRange(business, selfHelp, fantasy, history, kids, art,
            new Category { Name = "Romance" }, new Category { Name = "Music" }, new Category { Name = "Cooking" });

        var penguin = new Publisher { Name = "Penguin Books" };
        var harper = new Publisher { Name = "HarperCollins" };
        var scholastic = new Publisher { Name = "Scholastic" };
        var phaidon = new Publisher { Name = "Phaidon Press" };
        db.Publishers.AddRange(penguin, harper, scholastic, phaidon);

        Book Make(string title, string author, string desc, string img, decimal price, int year,
                  Category c, Publisher p, int stock, bool best, string? asin = null) => new()
                  {
                      Title = title,
                      Author = author,
                      Description = desc,
                      ImageUrl = Img + img,
                      Price = price,
                      Year = year,
                      Category = c,
                      Publisher = p,
                      Stock = stock,
                      IsBestSeller = best,
                      Asin = asin
                  };

        var richDad = Make("Rich Dad Poor Dad", "Robert T. Kiyosaki",
            "Two father figures, two views on money, and what the next generation can learn about building wealth.",
            "book-1.jpg", 45m, 1997, business, penguin, 50, true, "B09TWSRMCB");

        db.Books.AddRange(
            richDad,
            Make("The Psychology of Money", "Morgan Housel",
                "Short stories about how behavior, not intelligence, drives most financial outcomes.",
                "book-2.png", 28m, 2020, business, harper, 40, true),
            Make("Atomic Habits", "James Clear",
                "A practical system for building good habits and breaking bad ones through tiny daily changes.",
                "book-3.jpg", 32m, 2018, selfHelp, penguin, 60, true),
            Make("The Alchemist", "Paulo Coelho",
                "A shepherd boy travels in search of treasure and finds out what the journey is really about.",
                "book-4.jpg", 20m, 1988, fantasy, harper, 35, false),
            Make("Sapiens", "Yuval Noah Harari",
                "A sweeping look at how humans came to dominate the planet, from the Stone Age to today.",
                "book-5.jpg", 35m, 2011, history, harper, 25, false),
            Make("The Hobbit", "J.R.R. Tolkien",
                "Bilbo Baggins is swept into a dragon-guarded treasure quest he never planned to join.",
                "book-6.jpg", 25m, 1937, fantasy, harper, 30, false),
            Make("The Little Prince", "Antoine de Saint-Exupery",
                "A pilot stranded in the desert meets a young prince who sees the world very differently.",
                "book-7.png", 15m, 1943, kids, scholastic, 80, false),
            Make("The Story of Art", "E. H. Gombrich",
                "A clear, readable tour of art history from cave paintings to the modern era.",
                "book-8.png", 45m, 1950, art, phaidon, 15, false));

        var now = DateTime.UtcNow;
        db.FlashSales.Add(new FlashSale
        {
            Book = richDad,
            SalePrice = 30m,
            StartsAt = now.AddHours(-1),
            EndsAt = now.AddHours(30),
            StockLimit = 4
        });

        db.DiscountCodes.AddRange(
            new DiscountCode { Code = "Ne212", Percent = 25, Book = richDad },
            new DiscountCode { Code = "WELCOME10", Percent = 10 });

        await db.SaveChangesAsync();
    }
}