using Dfc.ProviderPortal.TribalExporter.Interfaces;
using Dfc.ProviderPortal.TribalExporter.Settings;
using Microsoft.WindowsAzure.Storage.Blob;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dfc.ProviderPortal.TribalExporter.Helpers
{
    public class MigrationProviderFileHelper : IMigrationProviderFileHelper
    {
        private readonly IBlobStorageHelper _blobStorageHelper;

        public MigrationProviderFileHelper(IBlobStorageHelper blobStorageHelper)
        {
            if (blobStorageHelper == null) throw new ArgumentNullException(nameof(blobStorageHelper));

            _blobStorageHelper = blobStorageHelper;
        }

        public async Task<IEnumerable<IMiragtionProviderItem>> GetItemsAsync(CloudBlobContainer container, string fileName)
        {
            var items = new List<MiragtionProviderItem>();
            var delimitedFileSettings = new DelimitedFileSettings(true);
            var content = await _blobStorageHelper.ReadFileAsync(container, fileName);
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
