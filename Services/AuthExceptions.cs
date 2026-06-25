namespace ResumeAnalyzer.Api.Services;

/// <summary>Thrown when signup is attempted with an email that's already registered.</summary>
public class EmailAlreadyInUseException : Exception
{
    public EmailAlreadyInUseException() : base("An account with this email already exists.")
    {
    }
}

/// <summary>Thrown when login credentials don't match a known account.</summary>
public class InvalidCredentialsException : Exception
{
    public InvalidCredentialsException() : base("Invalid email or password.")
    {
    }
}

/// <summary>
/// Thrown when an action is attempted using a valid JWT whose underlying
/// account has since been soft-deleted (see AuthOrchestrator.DeleteAccountAsync).
/// JWTs remain cryptographically valid for their full lifetime regardless of
/// account status, so this check happens at the point of use rather than at
/// authentication.
/// </summary>
public class AccountDeletedException : Exception
{
    public AccountDeletedException() : base("This account has been deleted.")
    {
    }
}
