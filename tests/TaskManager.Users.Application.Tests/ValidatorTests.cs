using TaskManager.SharedKernel;
using TaskManager.Users.Application.Login;
using TaskManager.Users.Application.Register;

namespace TaskManager.Users.Application.Tests;

public class ValidatorTests
{
    [Fact]
    public void RegisterValidate_validCommand_returnsNull()
        => RegisterUserValidator.Validate(new RegisterUserCommand("Ana", "ana@x.com", "Secret123")).ShouldBeNull();

    [Theory, InlineData("short1"), InlineData("allletters"), InlineData("12345678")]
    public void RegisterValidate_weakPassword_returnsPasswordError(string pw)
    {
        var error = RegisterUserValidator.Validate(new RegisterUserCommand("Ana", "ana@x.com", pw));

        error.ShouldNotBeNull();
        error.Type.ShouldBe(ErrorType.Validation);
        error.FieldErrors!["password"].ShouldNotBeEmpty();
    }

    [Fact]
    public void RegisterValidate_passwordOver128Characters_returnsMaxLengthError()
    {
        var error = RegisterUserValidator.Validate(new RegisterUserCommand("Ana", "ana@x.com", new string('a', 128) + "1"));

        error!.FieldErrors!["password"].ShouldContain("Password must be at most 128 characters.");
    }

    [Fact]
    public void RegisterValidate_allFieldsInvalid_collectsEveryFieldError()
    {
        var error = RegisterUserValidator.Validate(new RegisterUserCommand(" ", "nope", "x"));

        error.ShouldNotBeNull();
        error.Code.ShouldBe("validation_failed");
        error.FieldErrors!.Keys.ShouldBe(["fullName", "email", "password"], ignoreOrder: true);
    }

    [Fact]
    public void RegisterValidate_missingPassword_returnsRequiredError()
    {
        var error = RegisterUserValidator.Validate(new RegisterUserCommand("Ana", "ana@x.com", null));

        error!.FieldErrors!["password"].ShouldContain("Password is required.");
    }

    [Fact]
    public void LoginValidate_blankFields_requiresEmailAndPassword()
    {
        var error = LoginValidator.Validate(new LoginCommand(null, ""));

        error!.FieldErrors!["email"].ShouldBe(["Email is required."]);
        error.FieldErrors["password"].ShouldBe(["Password is required."]);
    }

    [Fact]
    public void LoginValidate_validCommand_returnsNull()
        => LoginValidator.Validate(new LoginCommand("a@b.c", "x")).ShouldBeNull();
}
