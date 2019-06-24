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
        public static IEnumerable<IMiragtionProviderItem> GetMiragtionProviderItems(string content, StringBuilder logger)
        {
            logger.AppendLine($"Inside {nameof(GetMiragtionProviderItems)}");
            logger.AppendLine($"Value for parameter: {nameof(content)} [content length: {content.Length}]");
            logger.Append(content);

            var items = new List<MiragtionProviderItem>();
            var delimitedFileSettings = new DelimitedFileSettings(true);

            logger.AppendLine($"Attempting to get delimited lines from content.");

            var delimitedLines = DelimitedFileHelper.ReadLines(new StringReader(content), delimitedFileSettings).ToList();

            logger.AppendLine($"Got {delimitedLines.Count} delimited lines from content.");

            logger.AppendLine($"{nameof(delimitedFileSettings.IsFirstRowHeaders)} = {delimitedFileSettings.IsFirstRowHeaders}");

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