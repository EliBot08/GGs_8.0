using FluentValidation;
using GGs.Shared.Api;
using GGs.Shared.Enums;

namespace GGs.Server.Validators;

public sealed class CreateTweakRequestValidator : AbstractValidator<CreateTweakRequest>
{
    public CreateTweakRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Category).MaximumLength(100).When(x => !string.IsNullOrWhiteSpace(x.Category));

        When(x => x.CommandType == CommandType.Registry, () =>
        {
            RuleFor(x => x.RegistryPath).NotEmpty();
            RuleFor(x => x.RegistryValueName).NotEmpty();
        });

        When(x => x.CommandType == CommandType.Service, () =>
        {
            RuleFor(x => x.ServiceName).NotEmpty();
            RuleFor(x => x.ServiceAction).NotNull();
        });

        When(x => x.CommandType == CommandType.Script, () =>
        {
            RuleFor(x => x.ScriptContent).NotEmpty();
        });

        // If undo is allowed and script was used, require undo script content
        When(x => x.AllowUndo && x.CommandType == CommandType.Script, () =>
        {
            RuleFor(x => x.UndoScriptContent).NotEmpty();
        });
    }
}

