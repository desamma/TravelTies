using DataAccess.Repositories.IRepositories;
using Models.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using System.Text;

namespace TravelTies.Areas.Identity.Controllers;

[Area("Identity")]
public class AuthController : Controller
{
    private readonly SignInManager<User> _signInManager;
    private readonly UserManager<User> _userManager;
    private readonly IUserRepository _userRepository;
    private readonly IEmailSender _emailSender;

    public AuthController(SignInManager<User> signInManager, UserManager<User> userManager, IUserRepository userRepository, IEmailSender emailSender)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _userRepository = userRepository;
        _emailSender = emailSender;
    }

    public async Task<IActionResult> ResendConfirmationEmail(string email, string? returnUrl = "~/")
    {
        if (string.IsNullOrEmpty(email))
        {
            return BadRequest("Email is required.");
        }

        try
        {
            var user = await _userManager.FindByEmailAsync(email);
            var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
            var callBackUrl = Url.Page(
                "/Account/ConfirmEmail",
                pageHandler: null,
                values: new { area = "Identity", userId = user.Id, code = code, returnUrl = returnUrl },
                protocol: Request.Scheme);
            await _emailSender.SendEmailAsync(email, "Confirm your email",
                $"Please confirm your account by <a href='{callBackUrl}'>clicking here</a>.");
        }
        catch (Exception ex)
        {
            // Log the error
            Console.WriteLine($"An error occurred while sending confirmation email: {ex.Message}");
            return StatusCode(500, "Internal server error while sending confirmation email.");
        }
        return Ok("Confirmation email resent successfully. Please check your inbox.");
    }

    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
        await HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
        HttpContext.Session.Clear(); // Clear the session
        await _signInManager.SignOutAsync();

        return RedirectToAction(nameof(Index), "Home",new { area = "User" });
    }
}