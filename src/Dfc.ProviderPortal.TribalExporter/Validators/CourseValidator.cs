using Dfc.CourseDirectory.Models.Models.Apprenticeships;
using Dfc.CourseDirectory.Models.Models.Courses;
using FluentValidation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Dfc.ProviderPortal.TribalExporter.Validators
{
    public class CourseValidator : AbstractValidator<Course>
    {
        public CourseValidator()
        {

            RuleFor(a => a.QualificationCourseTitle)
                            .Must(b => ValidationHelper.HasNoSpecialCharacters(b))
                            .WithMessage($"QualificationCourseTitle contains invalid characters.")
                            .When(a => !string.IsNullOrWhiteSpace(a.QualificationCourseTitle));
            RuleFor(a => a.CourseDescription)
                            .Must(b => ValidationHelper.HasNoSpecialCharacters(b))
                            .WithMessage($"CourseDescription contains invalid characters.")
                            .When(a => !string.IsNullOrWhiteSpace(a.CourseDescription));
            RuleFor(a => a.EntryRequirements)
                            .Must(b => ValidationHelper.HasNoSpecialCharacters(b))
                            .WithMessage($"EntryRequirements contains invalid characters.")
                            .When(a => !string.IsNullOrWhiteSpace(a.EntryRequirements));
            RuleFor(a => a.WhatYoullNeed)
                            .Must(b => ValidationHelper.HasNoSpecialCharacters(b))
                            .WithMessage($"WhatYoullNeed contains invalid characters.")
                            .When(a => !string.IsNullOrWhiteSpace(a.WhatYoullNeed));
            RuleFor(a => a.HowYoullBeAssessed)
                            .Must(b => ValidationHelper.HasNoSpecialCharacters(b))
                            .WithMessage($"HowYoullBeAssessed contains invalid characters.")
                            .When(a => !string.IsNullOrWhiteSpace(a.HowYoullBeAssessed));
            RuleFor(a => a.HowYoullBeAssessed)
                            .Must(b => ValidationHelper.HasNoSpecialCharacters(b))
                            .WithMessage($"HowYoullBeAssessed contains invalid characters.")
                            .When(a => !string.IsNullOrWhiteSpace(a.HowYoullBeAssessed));

            RuleForEach(a => a.CourseRuns).SetValidator(new CourseRunValidator());
        }
    }

    public class CourseRunValidator : AbstractValidator<CourseRun>
    {
        public CourseRunValidator()
        {
            RuleFor(r => r.CourseName)
                            .Must(b => ValidationHelper.HasNoSpecialCharacters(b))
                            .WithMessage($"CourseName contains invalid characters.")
                            .When(r => !string.IsNullOrWhiteSpace(r.CourseName));

            RuleFor(r => r.ProviderCourseID)
                            .Matches(ValidationHelper.UrlRegex)
                            .When(r => !string.IsNullOrWhiteSpace(r.ProviderCourseID));

            RuleFor(r => r.CourseURL)
                .Matches(ValidationHelper.UrlRegex)
                .When(r => !string.IsNullOrWhiteSpace(r.CourseURL));

            RuleFor(r => r.CostDescription)
                .Matches(ValidationHelper.UrlRegex)
                .When(r => !string.IsNullOrWhiteSpace(r.CostDescription));
        }
    }
}

