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
            logger.AppendLine($"Start {nameof(GetMiragtionProviderItems)}");
            logger.AppendLine($"Value for parameter: {nameof(content)} [content length: {content.Length}]");
            logger.Append(content);

            var items = new List<MiragtionProviderItem>();
            var delimitedFileSettings = new DelimitedFileSettings(true);

            logger.AppendLine($"Attempting to get delimited lines from content.");

            var delimitedLines = DelimitedFileHelper.ReadLines(new StringReader(content), delimitedFileSettings).ToList();

            logger.AppendLine($"Got {delimitedLines.Count} delimited lines from content.");

            logger.AppendLine($"{nameof(delimitedFileSettings.IsFirstRowHeaders)} = {delimitedFileSettings.IsFirstRowHeaders}");

            if (delimitedFileSettings.IsFirstRowHeaders) delimitedLines.RemoveAt(0);

            logger.AppendLine($"Number of delimeted lines {delimitedLines.Count}");

            foreach (var dl in delimitedLines)
            {
                logger.AppendLine($"Start foreach");

                var ukprnField = dl.Fields[0];
                var dateField = dl.Fields[1];

                logger.AppendLine($"{nameof(ukprnField)} = {ukprnField.Value}, {nameof(dateField)} = {dateField.Value}");

                if (ValuesAreValid(ukprnField.Value, dateField.Value))
                {
                    logger.AppendLine($"Start if");

                    int.TryParse(ukprnField.Value, out int ukprn);
                    DateTime.TryParse(dateField.Value, out DateTime date);
                    var item = new MiragtionProviderItem
                    {
                        Ukprn = ukprn,
                        DateMigrated = date
                    };

                    items.Add(item);

                    logger.AppendLine(item.ToString());

                    logger.AppendLine($"End if");
                }

                logger.AppendLine($"End foreach");
            }

            logger.AppendLine($"End {nameof(GetMiragtionProviderItems)}");

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