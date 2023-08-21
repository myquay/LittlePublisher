using IndieAuth;
using IndieAuth.Authentication;
using IndieAuth.Extensions;
using LittlePublisher.Web.Infrastructure;
using Microformats;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Diagnostics.Metrics;
using System.Text.Json;

namespace LittlePublisher.Web.Pages.Account
{
    public class SignInModel : PageModel
    {
        HttpClient client;

        [BindProperty]
        public string? Me { get; set; }

        [BindProperty]
        public string? ReturnUrl { get; set; }

        public SignInModel(HttpClient client)
        {
            this.client = client;
        }

        public async Task<IActionResult> OnGet(string? me = null, string? returnUrl = null)
        {
            Me = me?.Canonicalize();
            ReturnUrl = returnUrl;

            var user = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            if (!user.Succeeded)
            {
                return Page();
            }
            else
            {
                return Redirect(returnUrl ?? "/");
            }
        }

        public async Task<IActionResult> OnPost()
        {
            Me = Me?.Canonicalize();
            var user = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            if (!user.Succeeded)
            {
                if(String.IsNullOrEmpty(Me) || !Uri.IsWellFormedUriString(Me, UriKind.Absolute))
                    ModelState.AddModelError("me", "You must provide a valid URL");

                if (!ModelState.IsValid)
                    return await OnGet(Me, ReturnUrl);

                return Challenge(new IndieAuthChallengeProperties
                {
                    Me = Me!,
                    Scope = new[] { "profile", "create" },
                    RedirectUri = ReturnUrl
                }, IndieAuthDefaults.AuthenticationScheme);
            }
            else
            {
                return Redirect(ReturnUrl ?? "/");
            }
        }
    }
}
