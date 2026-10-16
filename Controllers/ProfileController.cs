using BookStore.Api.Domain;
using BookStore.Api.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace BookStore.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/profile")]
public class ProfileController(UserManager<ApplicationUser> users) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<UserDto>> Get()
    {
        var user = await users.FindByIdAsync(User.GetUserId());
        if (user is null) return NotFound();
        return UserDto.From(user, await users.GetRolesAsync(user));
    }

    [HttpPut]
    public async Task<ActionResult<UserDto>> Update(UpdateProfileRequest request)
    {
        var user = await users.FindByIdAsync(User.GetUserId());
        if (user is null) return NotFound();

        var email = request.Email.Trim();
        var existing = await users.FindByEmailAsync(email);
        if (existing is not null && existing.Id != user.Id)
            return Conflict(new ProblemDetails { Title = "Email is already registered.", Status = 409 });

        user.FullName = request.FullName.Trim();
        user.Email = email;
        user.UserName = email;
        user.PhoneNumber = request.PhoneNumber;
        user.Address = request.Address;
        user.ProfileImageUrl = request.ProfileImageUrl;

        var result = await users.UpdateAsync(user);
        if (!result.Succeeded)
        {
            foreach (var e in result.Errors) ModelState.AddModelError(e.Code, e.Description);
            return ValidationProblem(ModelState);
        }

        return UserDto.From(user, await users.GetRolesAsync(user));
    }

    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request)
    {
        var user = await users.FindByIdAsync(User.GetUserId());
        if (user is null) return NotFound();

        var result = await users.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
        {
            foreach (var e in result.Errors) ModelState.AddModelError(e.Code, e.Description);
            return ValidationProblem(ModelState);
        }

        return NoContent();
    }
}