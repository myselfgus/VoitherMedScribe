using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace MedicalScribeR.Web.Controllers
{
    [Route("[controller]")]
    public class AccountController : Controller
    {
        [HttpGet("SignIn")]
        public IActionResult SignIn()
        {
            var redirectUrl = Url.Action("Index", "Home");
            return Challenge(
                new AuthenticationProperties { RedirectUri = redirectUrl },
                OpenIdConnectDefaults.AuthenticationScheme);
        }

        [HttpGet("SignOut")]
        public async Task<IActionResult> SignOut()
        {
            var callbackUrl = Url.Action("SignedOut", "Account", values: null, protocol: Request.Scheme);
            await HttpContext.SignOutAsync(OpenIdConnectDefaults.AuthenticationScheme,
                new AuthenticationProperties { RedirectUri = callbackUrl });
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return LocalRedirect(callbackUrl);
        }

        [HttpGet("SignedOut")]
        public IActionResult SignedOut()
        {
            return View();
        }

        [HttpGet("AccessDenied")]
        public IActionResult AccessDenied()
        {
            return View();
        }
    }
}