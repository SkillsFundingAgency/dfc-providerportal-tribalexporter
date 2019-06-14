using Dfc.ProviderPortal.TribalExporter.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace Dfc.ProviderPortal.TribalExporter.Settings
{
    public class DelimitedFileSettings : IDelimitedFileSettings
    {
        private const char DELIMITED_CHARACTER = ',';
        private const bool IS_FIRST_ROW_HEADERS = false;

        public char DelimitingCharacter { get; set; }
        public bool IsFirstRowHeaders { get; set; }

        public DelimitedFileSettings()
            : this(DELIMITED_CHARACTER, IS_FIRST_ROW_HEADERS) { }

        public DelimitedFileSettings(bool isFirstRowHeaders)
            : this(DELIMITED_CHARACTER, isFirstRowHeaders) { }

        public DelimitedFileSettings(char delimitingCharacter)
            : this(delimitingCharacter, IS_FIRST_ROW_HEADERS) { }

        public DelimitedFileSettings(
            char delimitingCharacter,
            bool isFirstRowHeaders)
        {
            DelimitingCharacter = delimitingCharacter;
            IsFirstRowHeaders = isFirstRowHeaders;
        }
    }
}
