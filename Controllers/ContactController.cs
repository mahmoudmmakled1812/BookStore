using BookStore.Api.Data;
using BookStore.Api.Domain;
using BookStore.Api.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace BookStore.Api.Controllers;

[ApiController]
[Route("api/contact")]
public class ContactController(AppDbContext db) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Send(ContactRequest request)
    {
        db.ContactMessages.Add(new ContactMessage
        {
            Name = request.Name.Trim(),
            Email = request.Email.Trim(),
            Subject = request.Subject?.Trim(),
            Message = request.Message.Trim()
        });
        await db.SaveChangesAsync();
        return Accepted(new { message = "Thanks! Your message has been received." });
    }
}