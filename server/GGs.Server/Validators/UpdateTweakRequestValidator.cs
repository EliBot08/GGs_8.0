using FluentValidation;
using GGs.Shared.Api;

namespace GGs.Server.Validators;

public sealed class UpdateTweakRequestValidator : AbstractValidator<UpdateTweakRequest>
{
    public UpdateTweakRequestValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        Include(new CreateTweakRequestValidator());
    }
}

