using Dfc.ProviderPortal.TribalExporter.Interfaces;
using Dfc.ProviderPortal.TribalExporter.Settings;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;

namespace Dfc.ProviderPortal.TribalExporter.Helpers
{
    public static class MigrationProviderItemHelper
    {
        public static IEnumerable<IMiragtionProviderItem> GetMiragtionProviderItems(string content)
        {
            var items = new List<MiragtionProviderItem>();
            var delimitedFileSettings = new DelimitedFileSettings(true);
            var delimitedLines = DelimitedFileHelper.ReadLines(new StringReader(content), delimitedFileSettings).ToList();
            if (delimitedFileSettings.IsFirstRowHeaders) delimitedLines.RemoveAt(0);

            foreach (var dl in delimitedLines)
            {
                var ukprnField = dl.Fields[0];
                var dateField = dl.Fields[1];

                if (ValuesAreValid(ukprnField.Value, dateField.Value))
                {
                    int.TryParse(ukprnField.Value, out int ukprn);
                    DateTime.TryParse(dateField.Value, out DateTime date);

                    items.Add(new MiragtionProviderItem
                    {
                        Ukprn = ukprn,
                        DateMigrated = date
                    });
                }
            }

            return items;
        }

        internal static bool ValuesAreValid(string ukprnString, string dateString)
        {
            if (string.IsNullOrWhiteSpace(ukprnString)
                || string.IsNullOrWhiteSpace(dateString)
                || !int.TryParse(ukprnString, out int ukprn)
                || !DateTime.TryParse(dateString, out DateTime date)
                || ukprn < 1)
            {
                return false;
            }

            return true;
        }
    }
}