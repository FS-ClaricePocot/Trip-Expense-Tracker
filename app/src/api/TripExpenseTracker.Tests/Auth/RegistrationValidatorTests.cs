using FluentAssertions;
using TripExpenseTracker.Core.Auth;

namespace TripExpenseTracker.Tests.Auth;

public class RegistrationValidatorTests
{
    private const string ValidEmail = "anna@x.com";
    private const string ValidPassword = "password1";

    [Fact]
    public void Password_of_7_characters_is_too_short_and_no_account_is_allowed()
    {
        var result = RegistrationValidator.Validate(ValidEmail, new string('a', 7));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new FieldError("password", "password_too_short"));
    }

    [Fact]
    public void Password_of_8_characters_is_accepted()
    {
        var result = RegistrationValidator.Validate(ValidEmail, new string('a', 8));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Password_of_128_characters_is_accepted()
    {
        var result = RegistrationValidator.Validate(ValidEmail, new string('a', 128));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Password_of_129_characters_is_too_long()
    {
        var result = RegistrationValidator.Validate(ValidEmail, new string('a', 129));

        result.Errors.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new FieldError("password", "password_too_long"));
    }

    [Fact]
    public void Password_of_128_emoji_is_accepted_because_each_emoji_counts_as_one()
    {
        // Each emoji is two UTF-16 units, so this is 256 units but 128 characters.
        var password = string.Concat(Enumerable.Repeat("\U0001F600", 128));

        password.Length.Should().Be(256);
        RegistrationValidator.Validate(ValidEmail, password).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Password_of_129_emoji_is_too_long()
    {
        var password = string.Concat(Enumerable.Repeat("\U0001F600", 129));

        RegistrationValidator.Validate(ValidEmail, password).Errors.Should().ContainSingle()
            .Which.Code.Should().Be("password_too_long");
    }

    [Fact]
    public void Spaces_inside_a_password_are_allowed_and_not_trimmed()
    {
        var password = "  two words  ";

        var result = RegistrationValidator.Validate(ValidEmail, password);

        result.IsValid.Should().BeTrue();
        PasswordPolicy.CountCharacters(password).Should().Be(13);
    }

    [Fact]
    public void Email_is_trimmed_and_lower_cased()
    {
        var result = RegistrationValidator.Validate(" Anna@x.com ", ValidPassword);

        result.IsValid.Should().BeTrue();
        result.NormalizedEmail.Should().Be("anna@x.com");
    }

    [Fact]
    public void Email_without_an_at_sign_is_invalid()
    {
        var result = RegistrationValidator.Validate("anna.x.com", ValidPassword);

        result.Errors.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new FieldError("email", "email_invalid"));
    }

    [Fact]
    public void Email_with_two_at_signs_is_invalid()
    {
        RegistrationValidator.Validate("a@b@x.com", ValidPassword).Errors.Should().ContainSingle()
            .Which.Code.Should().Be("email_invalid");
    }

    [Fact]
    public void Email_with_no_dot_after_the_at_sign_is_invalid()
    {
        RegistrationValidator.Validate("anna@xcom", ValidPassword).Errors.Should().ContainSingle()
            .Which.Code.Should().Be("email_invalid");
    }

    [Fact]
    public void Empty_email_is_required()
    {
        RegistrationValidator.Validate("", ValidPassword).Errors.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new FieldError("email", "email_required"));
    }

    [Fact]
    public void Email_of_only_spaces_is_required()
    {
        RegistrationValidator.Validate("   ", ValidPassword).Errors.Should().ContainSingle()
            .Which.Code.Should().Be("email_required");
    }

    [Fact]
    public void Bad_email_and_short_password_report_both_fields()
    {
        var result = RegistrationValidator.Validate("anna.x.com", "short");

        result.Errors.Select(e => e.Field).Should().BeEquivalentTo(new[] { "email", "password" });
    }
}
