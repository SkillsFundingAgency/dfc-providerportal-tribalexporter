using Dfc.CourseDirectory.Models.Models.Apprenticeships;
using FluentValidation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Dfc.ProviderPortal.TribalExporter.Validators
{
    public class ApprenticeshipValidator : AbstractValidator<Apprenticeship>
    {
        public ApprenticeshipValidator()
        {

            RuleFor(a => a.ApprenticeshipTitle)
                            .Must(b => ValidationHelper.HasNoSpecialCharacters(b))
                            .WithMessage($"ApprenticeshipTitle contains invalid characters.")
                            .When(a => !string.IsNullOrWhiteSpace(a.ApprenticeshipTitle));
            RuleFor(a => a.MarketingInformation)
                            .Must(b => ValidationHelper.HasNoSpecialCharacters(b))
                            .WithMessage($"MarketingInformation contains invalid characters.")
                            .When(a => !string.IsNullOrWhiteSpace(a.MarketingInformation));
            RuleFor(a => a.Url)
                            .Matches(ValidationHelper.UrlRegex)
                            .When(a => !string.IsNullOrWhiteSpace(a.Url));
            RuleFor(a => a.ContactTelephone)
                            .Matches(ValidationHelper.UkTelephoneRegex)
                            .When(a => !string.IsNullOrWhiteSpace(a.ContactTelephone));
            RuleFor(a => a.ContactEmail)
                            .EmailAddress()
                            .When(a => !string.IsNullOrWhiteSpace(a.ContactEmail));
            RuleFor(a => a.ContactWebsite)
                            .Matches(ValidationHelper.UrlRegex)
                            .When(a => !string.IsNullOrWhiteSpace(a.ContactWebsite));
        }
    }
}

