using FluentValidation;
using GGs.Shared.Api;

namespace GGs.Server.Validators;

public sealed class LicenseIssueRequestValidator : AbstractValidator<LicenseIssueRequest>
{
    public LicenseIssueRequestValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.DeviceBindingId).MaximumLength(256).When(x => !string.IsNullOrWhiteSpace(x.DeviceBindingId));
        When(x => !x.IsAdminKey, () =>
        {
            RuleFor(x => x.ExpiresUtc)
                .NotNull()
                .Must(exp => exp!.Value > DateTime.UtcNow)
                .WithMessage("ExpiresUtc must be in the future");
        });
    }
}

