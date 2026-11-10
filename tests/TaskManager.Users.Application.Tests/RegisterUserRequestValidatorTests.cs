using TaskManager.SharedKernel;
using TaskManager.Users.Application.Contracts;
using TaskManager.Users.Application.Validation;

namespace TaskManager.Users.Application.Tests;

public class RegisterUserRequestValidatorTests
{
    [Fact]
    public void Valid_request_returns_null()
        => RegisterUserRequestValidator.Validate(new RegisterUserRequest("Ana", "ana@x.com", "Secret123")).ShouldBeNull();

    [Theory, InlineData("short1"), InlineData("allletters"), InlineData("12345678")]
    public void Register_rejects_weak_password(string pw)
    {
        var error = RegisterUserRequestValidator.Validate(new RegisterUserRequest("Ana", "ana@x.com", pw));

        error.ShouldNotBeNull();
        error.Type.ShouldBe(ErrorType.Validation);
        error.FieldErrors!["password"].ShouldNotBeEmpty();
    }

    [Fact]
    public void Register_rejects_password_over_128_characters()
    {
        var error = RegisterUserRequestValidator.Validate(new RegisterUserRequest("Ana", "ana@x.com", new string('a', 128) + "1"));

        error!.FieldErrors!["password"].ShouldContain("Password must be at most 128 characters.");
    }

    [Fact]
    public void Register_collects_errors_for_all_invalid_fields()
    {
        var error = RegisterUserRequestValidator.Validate(new RegisterUserRequest(" ", "nope", "x"));

        error.ShouldNotBeNull();
        error.Code.ShouldBe("validation_failed");
        error.FieldErrors!.Keys.ShouldBe(["fullName", "email", "password"], ignoreOrder: true);
    }

    [Fact]
    public void Register_requires_password()
    {
        var error = RegisterUserRequestValidator.Validate(new RegisterUserRequest("Ana", "ana@x.com", null));

        error!.FieldErrors!["password"].ShouldContain("Password is required.");
    }

    [Fact]
    public void Login_requires_email_and_password()
    {
        var error = LoginRequestValidator.Validate(new LoginRequest(null, ""));

        error!.FieldErrors!["email"].ShouldBe(["Email is required."]);
        error.FieldErrors["password"].ShouldBe(["Password is required."]);
    }

    [Fact]
    public void Login_valid_returns_null()
        => LoginRequestValidator.Validate(new LoginRequest("a@b.c", "x")).ShouldBeNull();
}
