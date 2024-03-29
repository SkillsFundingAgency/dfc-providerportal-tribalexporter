﻿using Dfc.CourseDirectory.Models.Interfaces.Providers;
using System;
using System.Collections.Generic;
using System.Text;

namespace Dfc.CourseDirectory.Models.Models.Providers
{
    public class Provider : IProvider
    {
        public Guid id { get; set; }
        public string UnitedKingdomProviderReferenceNumber { get; set; }
        public string ProviderName { get; set; }
        public string ProviderStatus { get; set; }
        public IProvidercontact[] ProviderContact { get; set; }
        public DateTime ProviderVerificationDate { get; set; }
        public bool ProviderVerificationDateSpecified { get; set; }
        public bool ExpiryDateSpecified { get; set; }
        public object ProviderAssociations { get; set; }
        public IProvideralias[] ProviderAliases { get; set; }
        public IVerificationdetail[] VerificationDetails { get; set; }
        public Status Status { get; set; }

        // Apprenticeship related
        public int? ProviderId { get; set; }
        public int? UPIN { get; set; } // Needed to get LearnerSatisfaction & EmployerSatisfaction from FEChoices        
        public string TradingName { get; set; }
        public bool NationalApprenticeshipProvider { get; set; }
        public string MarketingInformation { get; set; }
        public string Alias { get; set; }

        public bool PassedOverallQAChecks { get; set; }
        public bool RoATPFFlag { get; set; }

        public int? RoATPProviderTypeId { get; set; }

        public DateTime? RoATPStartDate { get; set; }

        public ProviderType ProviderType { get; set; }

        public string LastUpdatedBy { get; set; }
        public DateTime DateUpdated { get; set; }
       
        public Provider(Providercontact[] providercontact, Provideralias[] provideraliases, Verificationdetail[] verificationdetails)
        {
            ProviderContact = providercontact;
            ProviderAliases = provideraliases;
            VerificationDetails = verificationdetails;

        }
    }

    public enum ProviderType
    {
        Undefined = 0,
        FE = 1,
        Apprenticeship = 2,
        Both = FE | Apprenticeship
    }

    public enum Status
    {
        Registered = 0,
        Onboarded = 1,
        Unregistered = 2
    }
}
