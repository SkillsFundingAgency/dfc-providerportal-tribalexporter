﻿using Dfc.ProviderPortal.TribalExporter.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace Dfc.ProviderPortal.TribalExporter.Settings
{
    public class ApprenticeshipServiceSettings : IApprenticeshipServiceSettings
    {
        public string ApiUrl { get; set; }
        public string ApiKey { get; set; }
    }
}
