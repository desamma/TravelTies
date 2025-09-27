// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using DataAccess.Repositories.IRepositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using Models.Models;
using Newtonsoft.Json;
using Utilities.Constants;

namespace TravelTies.Areas.Identity.Pages.Account
{
    public class LoginModel : PageModel
    {
        private readonly SignInManager<User> _signInManager;
        private readonly ILogger<LoginModel> _logger;
        private readonly UserManager<User> _userManager;
        private readonly IUserRepository  _userRepository;

        public LoginModel(SignInManager<User> signInManager, ILogger<LoginModel> logger, UserManager<User> userManager, IUserRepository userRepository)
        {
            _signInManager = signInManager;
            _logger = logger;
            _userManager = userManager;
            _userRepository = userRepository;
        }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        [BindProperty]
        public InputModel Input { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public IList<AuthenticationScheme> ExternalLogins { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public string ReturnUrl { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        [TempData]
        public string ErrorMessage { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public class InputModel
        {
            /// <summary>
            ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
            ///     directly from your code. This API may change or be removed in future releases.
            /// </summary>
            [Required]
            [EmailAddress]
            public string Email { get; set; }

            /// <summary>
            ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
            ///     directly from your code. This API may change or be removed in future releases.
            /// </summary>
            [Required]
            [DataType(DataType.Password)]
            public string Password { get; set; }

            /// <summary>
            ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
            ///     directly from your code. This API may change or be removed in future releases.
            /// </summary>
            [Display(Name = "Remember me?")]
            public bool RememberMe { get; set; }
        }

        public async Task OnGetAsync(string returnUrl = null)
        {
            if (!string.IsNullOrEmpty(ErrorMessage))
            {
                ModelState.AddModelError(string.Empty, ErrorMessage);
            }

            returnUrl ??= Url.Content("~/");

            // Clear the existing external cookie to ensure a clean login process
            await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

            ReturnUrl = returnUrl;
        }

        public async Task<IActionResult> OnPostAsync(string returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");

            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

            if (ModelState.IsValid)
            {
                /*// This doesn't count login failures towards account lockout
                // To enable password failures to trigger account lockout, set lockoutOnFailure: true
                var result = await _signInManager.PasswordSignInAsync(Input.Email, Input.Password, Input.RememberMe, lockoutOnFailure: false);
                if (result.Succeeded)
                {
                    _logger.LogInformation("User logged in.");
                    return LocalRedirect(returnUrl);
                }
                if (result.RequiresTwoFactor)
                {
                    return RedirectToPage("./LoginWith2fa", new { ReturnUrl = returnUrl, RememberMe = Input.RememberMe });
                }
                if (result.IsLockedOut)
                {
                    _logger.LogWarning("User account locked out.");
                    return RedirectToPage("./Lockout");
                }
                else
                {
                    ModelState.AddModelError(string.Empty, "Invalid login attempt.");
                    return Page();
                }*/
                
                var user = await _signInManager.UserManager.FindByEmailAsync(Input.Email);
                if (user == null)
                {
                    ModelState.AddModelError(string.Empty, "Invalid login attempt.");
                }
                else
                {
                    if (await _userManager.IsInRoleAsync(user, RoleConstants.Banned))
                    {
                        ModelState.AddModelError(string.Empty, "Your account has been banned. Please contact support for more information.");
                        return Page();
                    }
                    var isEmailConfirmed = await _userManager.IsEmailConfirmedAsync(user);
                    if (!isEmailConfirmed)
                    {
                        var resendConfirmationUrl = Url.Action(
                            "ResendConfirmationEmail", "Auth", new { email = user.Email, returnUrl = returnUrl }, Request.Scheme);
                        ModelState.AddModelError(string.Empty, $"You must confirm your email before logging in. "+ resendConfirmationUrl);
                        return Page();
                    }
                }
                _logger.LogWarning("LOGIN: PasswordSigninAsync");
                var result = await _signInManager.PasswordSignInAsync(user, Input.Password, Input.RememberMe, false);
                if (!result.Succeeded)
                {
                    if (await _userManager.IsLockedOutAsync(user))
                    {
                        ModelState.AddModelError(string.Empty, "Your account has been locked out due to multiple failed login attempts. Please try again later.");
                        _logger.LogWarning("User account locked out.");
                        return Page();
                    }

                    ModelState.AddModelError(string.Empty, "Invalid login attempt. Please check your email and password.");
                    _logger.LogWarning("Invalid login attempt for user: {Email}", Input.Email);
                    return Page();
                }
                _logger.LogWarning("LOGIN: SET USER FOR SESSION");
                var currentUser = await _userRepository.GetAsync(u => u.Email == user.Email);
                HttpContext.Session.SetString("CurrentUser", JsonConvert.SerializeObject(user));
                // Just in case
                //HttpContext.Session.SetString("currentUserID", currentUser.Id.ToString());

                if (result.Succeeded)
                {
                    // Log the user login event
                    _logger.LogInformation("User {Email} logged in at {Time}.", Input.Email, DateTime.UtcNow);

                    // Redirect based on user role
                    if (await _userManager.IsInRoleAsync(user, RoleConstants.Admin))
                    {
                        returnUrl = Url.Action("Dashboard", "Admin", new { area = "Admin" });
                    }
                    else if (await _userManager.IsInRoleAsync(user, RoleConstants.User))
                    {
                        returnUrl = Url.Action(nameof(Index), "User", new { area = "User" });

                    }
                    else if (await _userManager.IsInRoleAsync(user, RoleConstants.Company))
                    {
                        returnUrl = Url.Action(nameof(Index), "Company", new { area = "Company" });
                    }
                    return LocalRedirect(returnUrl);
                }
            }
            // If we got this far, something failed, redisplay form
            return Page();
        }
    }
}
