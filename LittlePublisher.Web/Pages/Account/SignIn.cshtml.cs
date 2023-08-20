using IndieAuth;
using IndieAuth.Authentication;
using IndieAuth.Claims;
using IndieAuth.Discovery;
using LittlePublisher.Web.Infrastructure;
using Microformats;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text.Json;

namespace LittlePublisher.Web.Pages.Account
{
    public class SignInModel : PageModel
    {
        HttpClient client;

        public SignInModel(HttpClient client)
        {
            this.client = client;
        }

        public async Task<IActionResult> OnGet(string me, string? auth_method = null, string? returnUrl = null)
        {
            var user = await HttpContext.AuthenticateAsync(IndieAuthDefaults.ExternalCookieSignInScheme);

            if (!user.Succeeded)
            {
                //If we have a me value, we can use that to pre-populate the login form
                if (string.IsNullOrEmpty(me))
                    return Page();

                var authMethods = await new RelMeAuthDiscoveryClient(client).DiscoverSupportedAuthenticationMethods(me);
                if (auth_method != null)
                    authMethods = authMethods.Where(authMethods => authMethods == auth_method).ToArray();

                //No available auth methods
                if(!authMethods.Any())
                {
                    ModelState.AddModelError("me", $"The website '{me}' does not list any of the supported authentication methods ({String.Join(", ", authMethods)}).");
                    return Page();
                }

                //If we have multiple auth methods, we need to ask the user which one they want to use
                if (authMethods.Length > 1)
                    return RedirectToPage("ChooseAuthMethod", new { me });

                //Sign in using specified authentication method
                return Challenge(authMethods.Single());
            }
            else
            {
                return Redirect(returnUrl ?? "/");
            }
        }
    }
}
