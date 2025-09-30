using FluentValidation;
using GGs.Server.Controllers;

namespace GGs.Server.Validators;

public sealed class ScimUserValidator : AbstractValidator<ScimUser>
{
    public ScimUserValidator()
    {
        RuleFor(x => x.UserName)
            .NotEmpty()
            .WithMessage("UserName is required")
            .Length(3, 100)
            .WithMessage("UserName must be between 3 and 100 characters")
            .EmailAddress()
            .WithMessage("UserName must be a valid email address")
            .Matches(@"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$")
            .WithMessage("UserName contains invalid characters");

        RuleFor(x => x.Active)
            .NotNull()
            .WithMessage("Active status must be specified");

        RuleForEach(x => x.Emails)
            .SetValidator(new ScimEmailValidator())
            .When(x => x.Emails != null);

        RuleForEach(x => x.Groups)
            .SetValidator(new ScimGroupValidator())
            .When(x => x.Groups != null);

        RuleFor(x => x.Name)
            .SetValidator(new ScimNameValidator() as IValidator<ScimName?>)
            .When(x => x.Name != null);
    }
}

public sealed class ScimEmailValidator : AbstractValidator<ScimEmail>
{
    public ScimEmailValidator()
    {
        RuleFor(x => x.Value)
            .NotEmpty()
            .WithMessage("Email value is required")
            .EmailAddress()
            .WithMessage("Invalid email format")
            .Length(5, 254)
            .WithMessage("Email must be between 5 and 254 characters");

        RuleFor(x => x.Type)
            .NotEmpty()
            .WithMessage("Email type is required")
            .Must(type => new[] { "work", "home", "other" }.Contains(type.ToLowerInvariant()))
            .WithMessage("Email type must be 'work', 'home', or 'other'");
    }
}

public sealed class ScimGroupValidator : AbstractValidator<ScimGroup>
{
    private static readonly string[] ValidRoles = { "Owner", "Admin", "Moderator", "EnterpriseUser", "ProUser", "BasicUser" };

    public ScimGroupValidator()
    {
        RuleFor(x => x.Value)
            .NotEmpty()
            .WithMessage("Group value is required")
            .Must(value => ValidRoles.Contains(value))
            .WithMessage($"Group value must be one of: {string.Join(", ", ValidRoles)}");

        RuleFor(x => x.Display)
            .Length(0, 100)
            .WithMessage("Group display name cannot exceed 100 characters")
            .When(x => !string.IsNullOrEmpty(x.Display));
    }
}

public sealed class ScimNameValidator : AbstractValidator<ScimName>
{
    public ScimNameValidator()
    {
        RuleFor(x => x.FamilyName)
            .Length(0, 50)
            .WithMessage("Family name cannot exceed 50 characters")
            .Matches(@"^[a-zA-Z\s\-'\.]*$")
            .WithMessage("Family name contains invalid characters")
            .When(x => !string.IsNullOrEmpty(x.FamilyName));

        RuleFor(x => x.GivenName)
            .Length(0, 50)
            .WithMessage("Given name cannot exceed 50 characters")
            .Matches(@"^[a-zA-Z\s\-'\.]*$")
            .WithMessage("Given name contains invalid characters")
            .When(x => !string.IsNullOrEmpty(x.GivenName));

        RuleFor(x => x.MiddleName)
            .Length(0, 50)
            .WithMessage("Middle name cannot exceed 50 characters")
            .Matches(@"^[a-zA-Z\s\-'\.]*$")
            .WithMessage("Middle name contains invalid characters")
            .When(x => !string.IsNullOrEmpty(x.MiddleName));

        RuleFor(x => x.HonorificPrefix)
            .Length(0, 20)
            .WithMessage("Honorific prefix cannot exceed 20 characters")
            .When(x => !string.IsNullOrEmpty(x.HonorificPrefix));

        RuleFor(x => x.HonorificSuffix)
            .Length(0, 20)
            .WithMessage("Honorific suffix cannot exceed 20 characters")
            .When(x => !string.IsNullOrEmpty(x.HonorificSuffix));
    }
}
