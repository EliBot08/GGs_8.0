using FluentValidation;
using GGs.Server.Controllers;

namespace GGs.Server.Validators;

public sealed class LoginRequestValidator : AbstractValidator<AuthController.LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Username).NotEmpty();
        RuleFor(x => x.Password).NotEmpty();
    }
}

public sealed class RefreshRequestValidator : AbstractValidator<AuthController.RefreshRequest>
{
    public RefreshRequestValidator()
    {
        RuleFor(x => x.RefreshToken).NotEmpty();
    }
}

