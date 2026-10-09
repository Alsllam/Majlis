using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Majlis.Framework.Application.Services;

/// <summary>
/// Base for every AppService. AppServices are exposed as controllers by <c>AddDynamicControllers</c>;
/// set the route explicitly with <c>[Route("kebab-plural")]</c>. Authorized by default.
/// Public helpers that must not become endpoints get <c>[NonAction]</c> (the skill's <c>NonActionApi</c>; ASP.NET's attribute is sealed).
/// </summary>
[ApiController]
[Authorize]
[Produces("application/json")]
public abstract class ApplicationService : ControllerBase
{
    /// <summary>Validates once, explicitly, so async database rules never run twice.</summary>
    [NonAction]
    protected static async Task ValidateAsync<T>(IValidator<T> validator, T input, CancellationToken cancellationToken)
    {
        var result = await validator.ValidateAsync(input, cancellationToken);
        if (!result.IsValid)
        {
            throw new ValidationException(result.Errors);
        }
    }
}
