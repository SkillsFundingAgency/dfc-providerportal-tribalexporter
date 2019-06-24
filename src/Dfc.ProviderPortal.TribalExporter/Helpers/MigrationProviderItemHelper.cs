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

                if (ValuesAreValid(ukprnField.Value, dateField.Value, logger))
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

        internal static bool ValuesAreValid(string ukprnString, string dateString, StringBuilder logger)
        {
            var cond1 = string.IsNullOrWhiteSpace(ukprnString);
            var cond2 = string.IsNullOrWhiteSpace(dateString);
            var cond3 = !int.TryParse(ukprnString, out int ukprn);
            var cond4 = !DateTime.TryParse(dateString, out DateTime date);
            var cond5 = ukprn < 1;

            logger.AppendLine($"{nameof(ValuesAreValid)} CONDITION {nameof(cond1)} = {cond1}");
            logger.AppendLine($"{nameof(ValuesAreValid)} CONDITION {nameof(cond2)} = {cond2}");
            logger.AppendLine($"{nameof(ValuesAreValid)} CONDITION {nameof(cond3)} = {cond3}");
            logger.AppendLine($"{nameof(ValuesAreValid)} CONDITION {nameof(cond4)} = {cond4}");
            logger.AppendLine($"{nameof(ValuesAreValid)} CONDITION {nameof(cond5)} = {cond5}");

            if (cond1 || cond2 || cond2 || cond3 || cond4 || cond5)
            //if (string.IsNullOrWhiteSpace(ukprnString)
            //    || string.IsNullOrWhiteSpace(dateString)
            //    || !int.TryParse(ukprnString, out int ukprn)
            //    || !DateTime.TryParse(dateString, out DateTime date)
            //    || ukprn < 1)
            {
                logger.AppendLine($"{nameof(ValuesAreValid)} FALSE {nameof(ukprnString)} = {ukprnString}, {nameof(dateString)} = {dateString}");
                return false;
            }

            logger.AppendLine($"{nameof(ValuesAreValid)} TRUE {nameof(ukprnString)} = {ukprnString}, {nameof(dateString)} = {dateString}");
            return true;
        }
    }
}