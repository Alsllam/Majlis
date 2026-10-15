using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Majlis.Framework.Application.Localization;
using Majlis.Framework.Domain.Exceptions;
using Majlis.Identity.Application.Invitations;
using Majlis.Identity.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Majlis.Auth.Host.Pages.Account;

/// <summary>Account creation from an invitation link (FR-IDN-005): the email is fixed by the invitation.</summary>
public sealed class RegisterModel(InvitationAcceptance acceptance, SignInManager<MajlisUser> signIn, ILocalizer localizer) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? Token { get; set; }

    public Invitation? Invitation { get; private set; }

    public string? Error { get; private set; }

    public bool Done { get; private set; }

    public string Lang => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;

    public bool IsRtl => Lang == "ar";

    public ILocalizer L => localizer;

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Invitation = await acceptance.FindOpenAsync(Token ?? string.Empty, cancellationToken);
        ApplyLanguage(Invitation?.Language);
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        Invitation = await acceptance.FindOpenAsync(Token ?? string.Empty, cancellationToken);
        ApplyLanguage(Invitation?.Language);
        if (Invitation is null)
        {
            return Page();
        }

        if (!ModelState.IsValid)
        {
            Error = L["General:Fields:Invalid"];
            return Page();
        }

        try
        {
            var user = await acceptance.AcceptAsync(Token!, Input.DisplayName, Input.Password, cancellationToken);
            await signIn.SignInAsync(user, isPersistent: false);
            Done = true;
            return Page();
        }
        catch (MajlisException ex)
        {
            Error = L[ex.MessageKey, ex.Arguments];
            return Page();
        }
    }

    private static void ApplyLanguage(string? lang)
    {
        if (lang is "ar" or "en")
        {
            CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(lang);
        }
    }

    public sealed class InputModel
    {
        [Required]
        [StringLength(120, MinimumLength = 2)]
        public string DisplayName { get; set; } = string.Empty;

        [Required]
        [StringLength(128, MinimumLength = 10)]
        public string Password { get; set; } = string.Empty;
    }
}
