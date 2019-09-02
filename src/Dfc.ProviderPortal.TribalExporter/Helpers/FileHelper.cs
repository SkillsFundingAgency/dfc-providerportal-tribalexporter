using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Dfc.CourseDirectory.Services.Interfaces.BlobStorageService;

namespace Dfc.ProviderPortal.TribalExporter.Helpers
{
    public static class FileHelper
    {

        public static List<int> GetProviderUKPRNs(string filePath, string fileName, out string errorMessageGetCourses)
        {
            var providerUKPRNList = new List<int>();
            var count = 1;
            string errors = string.Empty;
            string ProviderSelectionsPath = string.Format(@"{0}", filePath);
            if (!Directory.Exists(ProviderSelectionsPath))
                Directory.CreateDirectory(ProviderSelectionsPath);
            string selectionOfProviderFile = string.Format(@"{0}\{1}", ProviderSelectionsPath, fileName);
            using (StreamReader reader = new StreamReader(selectionOfProviderFile))
            {
                string line = null;
                while (null != (line = reader.ReadLine()))
                {
                    try
                    {
                        string[] linedate = line.Split(',');

                        var provider = linedate[0];
                        var migrationdate = linedate[1];
                        DateTime migDate = DateTime.MinValue;
                        int provID = 0;
                        DateTime.TryParse(migrationdate, out migDate);
                        int.TryParse(provider, out provID);
                        if (migDate > DateTime.MinValue && migDate == DateTime.Today && provID > 0)
                            providerUKPRNList.Add(provID);

                    }
                    catch (Exception ex)
                    {
                        errors = errors + "Failed textract line: " + count.ToString() + "Ex: " + ex.Message;
                    }
                }
            }

            errorMessageGetCourses = errors;
            return providerUKPRNList;
        }

        public static List<int> GetProviderUKPRNsFromBlob(IBlobStorageService blobService, out string errorMessageGetCourses, int migrationHours)
        {
            var providerUKPRNList = new List<int>();
            var count = 1;
            string errors = string.Empty;

            MemoryStream ms = new MemoryStream();
            Task.Run(async () => await blobService.GetBulkUploadProviderListFileAsync(ms)).Wait();
            
            ms.Position = 0;

            using (StreamReader reader = new StreamReader(ms))
            {
                string line = null;
                while (null != (line = reader.ReadLine()))
                {
                    try
                    {
                        string[] linedate = line.Split(',');

                        var provider = linedate[0];
                        var migrationdate = linedate[1];
                        var time = string.IsNullOrEmpty(linedate[2]) ? DateTime.Now.ToShortTimeString() : linedate[2];
                        DateTime migDate = DateTime.MinValue;
                        DateTime runTime = DateTime.MinValue;
                        int provID = 0;
                        DateTime.TryParse(migrationdate, out migDate);
                        DateTime.TryParse(time, out runTime);
                        migDate = migDate.Add(runTime.TimeOfDay);
                        int.TryParse(provider, out provID);
                        if (migDate > DateTime.MinValue && DateTimeWithinSpecifiedTime(migDate, migrationHours) && provID > 0)
                            providerUKPRNList.Add(provID);

                    }
                    catch (Exception ex)
                    {
                        errors = errors + "Failed textract line: " + count.ToString() + "Ex: " + ex.Message;
                    }
                }
            }

            errorMessageGetCourses = errors;
            return providerUKPRNList;
        }

        private static bool DateTimeWithinSpecifiedTime(DateTime value, int hours)
        {
            return value <= DateTime.Now && value >= DateTime.Now.AddHours(-hours);
        } 
    }
}
