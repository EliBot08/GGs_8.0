using FluentValidation;
using GGs.Server.Controllers;

namespace GGs.Server.Validators;

public sealed class DeviceEnrollRequestValidator : AbstractValidator<DeviceEnrollRequest>
{
    public DeviceEnrollRequestValidator()
    {
        RuleFor(x => x.DeviceId).NotEmpty();
        RuleFor(x => x.Thumbprint).NotEmpty();
    }
}

