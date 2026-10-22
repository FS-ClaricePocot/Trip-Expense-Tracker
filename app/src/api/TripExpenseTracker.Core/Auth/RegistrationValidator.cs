namespace TripExpenseTracker.Core.Auth;

/// <summary>One failed field check. Code is a stable key; the front end maps it to copy.</summary>
public sealed record FieldError(string Field, string Code);

public sealed record RegistrationValidation(string NormalizedEmail, IReadOnlyList<FieldError> Errors)
{
    public bool IsValid => Errors.Count == 0;
}

/// <summary>
/// Checks registration input and returns the field codes from the F1 plan
/// (email_required, email_invalid, password_too_short, password_too_long).
/// Pure logic, no I/O.
/// </summary>
public static class RegistrationValidator
{
    public const string EmailField = "email";
    public const string PasswordField = "password";

    public static RegistrationValidation Validate(string? email, string? password)
    {
        var errors = new List<FieldError>();

        var normalized = EmailAddress.Normalize(email);
        if (normalized.Length == 0)
        {
            errors.Add(new FieldError(EmailField, "email_required"));
        }
        else if (!EmailAddress.IsWellFormed(normalized))
        {
            errors.Add(new FieldError(EmailField, "email_invalid"));
        }

        switch (PasswordPolicy.CheckLength(password))
        {
            case PasswordLengthResult.TooShort:
                errors.Add(new FieldError(PasswordField, "password_too_short"));
                break;
            case PasswordLengthResult.TooLong:
                errors.Add(new FieldError(PasswordField, "password_too_long"));
                break;
        }

        return new RegistrationValidation(normalized, errors);
    }
}
