using System;
using System.Collections.Generic;
using System.Text;

namespace Dfc.CourseDirectory.Common.Settings
{
    public class ExporterSettings
    {
        public DateTime ExporterStartDate { get; set; }
        public DateTime ExporterEndDate { get; set; }
        public string ContainerNameExporter { get; set; }
        public string ContainerNameProviderFiles { get; set; }
        public string MigrationProviderCsv { get; set; }
    }

    public class VenueNameComponentSettings
    {
        public string VenueName_Label { get; set; }
    }

    public class CourseForComponentSettings
    {
        public int TextFieldMaxChars { get; set; }
    }

    public class EntryRequirementsComponentSettings
    {
        public int TextFieldMaxChars { get; set; }
    }

    public class WhatWillLearnComponentSettings
    {
        public int TextFieldMaxChars { get; set; }
    }

    public class HowYouWillLearnComponentSettings
    {
        public int TextFieldMaxChars { get; set; }
    }

    public class WhatYouNeedComponentSettings
    {
        public int TextFieldMaxChars { get; set; }
    }

    public class HowAssessedComponentSettings
    {
        public int TextFieldMaxChars { get; set; }
    }

    public class WhereNextComponentSettings
    {
        public int TextFieldMaxChars { get; set; }
    }
}


