using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Dfc.ProviderPortal.TribalExporter.Validators
{
    public static class ValidationHelper
    {
        public const string UrlRegex = @"^(http:\/\/www\.|https:\/\/www\.|http:\/\/|https:\/\/)?[a-z0-9]+([\-\.]{1}[a-z0-9]+)*\.[a-z]{2,5}(:[0-9]{1,5})?(\/.*)?$";
        public const string UkTelephoneRegex = @"^(((\+44\s?\d{4}|\(?0\d{4}\)?)\s?\d{3}\s?\d{3})|((\+44\s?\d{3}|\(?0\d{3}\)?)\s?\d{3}\s?\d{4})|((\+44\s?\d{2}|\(?0\d{2}\)?)\s?\d{4}\s?\d{4}))(\s?\#(\d{4}|\d{3}))?$";

        public static bool HasNoSpecialCharacters(string text)
        {
            string specialChar = @"\|!#$%&/=?»«@£§€{};<>_,";
            foreach (var item in specialChar)
            {
                if (text.Contains(item))
                {
                      return false;
                }
            }

            return true;
        }

        public static bool MustMatchRegEx(string text, string regex)
        {
            Match match = Regex.Match(text, regex, RegexOptions.IgnoreCase);

            if (text != string.Empty && !match.Success)
            {
                return false;
            }

            return true;
        }
    }
}
