using FluentValidation;

namespace Reflow.Modules.Identity.Features.GoogleLogin;

public class GoogleLoginRequestValidator : AbstractValidator<GoogleLoginRequest>
{
    public GoogleLoginRequestValidator()
    {
        RuleFor(x => x)
            .Must(x => !string.IsNullOrWhiteSpace(x.IdToken) || !string.IsNullOrWhiteSpace(x.AccessToken))
            .WithMessage("Either IdToken or AccessToken is required.");
    }
}
