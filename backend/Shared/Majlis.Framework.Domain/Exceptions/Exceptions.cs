namespace Majlis.Framework.Domain.Exceptions;

/// <summary>Base for errors that are safe to show. <see cref="MessageKey"/> is a localization key.</summary>
public abstract class MajlisException : Exception
{
    protected MajlisException(string messageKey, params object[] arguments)
        : base(messageKey)
    {
        MessageKey = messageKey;
        Arguments = arguments;
    }

    public string MessageKey { get; }

    public object[] Arguments { get; }
}

/// <summary>400: a business rule was broken.</summary>
public sealed class CustomValidationException(string messageKey, params object[] arguments)
    : MajlisException(messageKey, arguments);

/// <summary>404.</summary>
public sealed class EntityNotFoundException(string messageKey = "General:Business:NotFound", params object[] arguments)
    : MajlisException(messageKey, arguments);

/// <summary>403.</summary>
public sealed class ForbiddenException(string messageKey = "General:Business:Forbidden", params object[] arguments)
    : MajlisException(messageKey, arguments);

/// <summary>409: the request conflicts with the current state (stale epoch, turn already running).</summary>
public sealed class ConflictException(string messageKey, params object[] arguments)
    : MajlisException(messageKey, arguments);

/// <summary>503: a dependency is unavailable.</summary>
public sealed class ServiceUnavailableException(string messageKey = "General:Errors:ServiceUnavailable", params object[] arguments)
    : MajlisException(messageKey, arguments);
