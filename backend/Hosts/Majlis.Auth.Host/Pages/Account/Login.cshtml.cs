using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Majlis.Framework.Application.Localization;
using Majlis.Identity.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Majlis.Auth.Host.Pages.Account;

public sealed class LoginModel(SignInManager<MajlisUser> signIn, UserManager<MajlisUser> users, ILocalizer localizer) : PageModel
{
    private const string LanguageCookie = "majlis.lang";

    [BindProperty]
    public InputModel Input { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public string? Error { get; private set; }

    public string Lang => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;

    public bool IsRtl => Lang == "ar";

    public ILocalizer L => localizer;

    public string OtherLanguageUrl => Url.Page("/Account/Login", new { ReturnUrl, lang = IsRtl ? "en" : "ar" })!;

    public void OnGet(string? lang) => ApplyLanguage(lang);

    public async Task<IActionResult> OnPostAsync(string? lang)
    {
        ApplyLanguage(lang);
        if (!ModelState.IsValid)
        {
            Error = L["Identity:Login:Invalid"];
            return Page();
        }

        var user = await users.FindByEmailAsync(Input.Email.Trim());
        if (user is { IsActive: false })
        {
            Error = L["Identity:Login:Inactive"];
            return Page();
        }

        var result = user is null
            ? Microsoft.AspNetCore.Identity.SignInResult.Failed
            : await signIn.PasswordSignInAsync(user, Input.Password, isPersistent: false, lockoutOnFailure: true);
        if (result.Succeeded)
        {
            // Only local return URLs (the authorize endpoint) are allowed.
            return LocalRedirect(Url.IsLocalUrl(ReturnUrl) ? ReturnUrl! : "/");
        }

        Error = result.IsLockedOut ? L["Identity:Login:LockedOut"] : L["Identity:Login:Invalid"];
        return Page();
    }

    private void ApplyLanguage(string? lang)
    {
        lang ??= Request.Cookies[LanguageCookie];
        if (lang is "ar" or "en")
        {
            Response.Cookies.Append(LanguageCookie, lang, new CookieOptions { HttpOnly = true, SameSite = SameSiteMode.Lax, MaxAge = TimeSpan.FromDays(365) });
            CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(lang);
        }
    }

    public sealed class InputModel
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string Password { get; set; } = string.Empty;
    }
}
