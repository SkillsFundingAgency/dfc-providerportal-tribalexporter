using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using Dfc.CourseDirectory.Models.Models.Apprenticeships;
using Dfc.CourseDirectory.Models.Models.Courses;
using Dfc.ProviderPortal.ApprenticeshipMigration.Models;
using Microsoft.Extensions.Logging;

namespace Dfc.ProviderPortal.ApprenticeshipMigration.Helpers
{
    public static class DataHelper
    {
        public static List<int> GetProviderUKPRNs(string connectionString, out string errorMessageGetCourses)
        {
            var ukprnList = new List<int>();
            errorMessageGetCourses = string.Empty;

            using (var sqlConnection = new SqlConnection(connectionString))
            {
                using (var command = sqlConnection.CreateCommand())
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.CommandText = "dfc_GetProviderUKPRNs";

                    try
                    {
                        //Open connection.
                        sqlConnection.Open();

                        using (SqlDataReader dataReader = command.ExecuteReader())
                        {
                            while (dataReader.Read())
                            {
                                int ukprn = (int)CheckForDbNull(dataReader["Ukprn"], 0);
                                if (ukprn != 0)
                                    ukprnList.Add(ukprn);
                            }
                            // Close the SqlDataReader.
                            dataReader.Close();
                        }
                    }
                    catch (Exception ex)
                    {
                        errorMessageGetCourses = string.Format("Error Message: {0}" + Environment.NewLine + "Stack Trace: {1}", ex.Message, ex.StackTrace);
                    }
                    finally
                    {
                        sqlConnection.Close();
                    }
                }
            }
            return ukprnList;
        }

        public static TribalProvider GetProviderDetailsByUKPRN(int ProviderUKPRN, string connectionString, out string errorMessageGetProviderDetailsByUKPRN)
        {
            var provider = new TribalProvider();
            errorMessageGetProviderDetailsByUKPRN = string.Empty;

            using (var sqlConnection = new SqlConnection(connectionString))
            {
                using (var command = sqlConnection.CreateCommand())
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.CommandText = "dfc_GetProviderDetailsByUKPRN";

                    command.Parameters.Add(new SqlParameter("@ProviderUKPRN", SqlDbType.Int));
                    command.Parameters["@ProviderUKPRN"].Value = ProviderUKPRN;

                    try
                    {
                        //Open connection.
                        sqlConnection.Open();

                        using (SqlDataReader dataReader = command.ExecuteReader())
                        {
                            while (dataReader.Read())
                            {
                                provider = ExtractProviderDetailsFromDbReader(dataReader);
                            }
                            // Close the SqlDataReader.
                            dataReader.Close();
                        }
                    }
                    catch (Exception ex)
                    {
                        errorMessageGetProviderDetailsByUKPRN = string.Format("Error Message: {0}" + Environment.NewLine + "Stack Trace: {1}", ex.Message, ex.StackTrace);
                    }
                    finally
                    {
                        sqlConnection.Close();
                    }
                }
            }

            return provider;
        }

        public static TribalProvider ExtractProviderDetailsFromDbReader(SqlDataReader reader)
        {
            TribalProvider provider = new TribalProvider();

            provider.ProviderId = (int)CheckForDbNull(reader["ProviderId"], 0);
            provider.ProviderName = (string)CheckForDbNull(reader["ProviderName"], string.Empty);
            provider.ProviderNameAlias = (string)CheckForDbNull(reader["ProviderNameAlias"], string.Empty);
            provider.TradingName = (string)CheckForDbNull(reader["TradingName"], string.Empty);
            provider.UnitedKingdomProviderReferenceNumber = ((int)CheckForDbNull(reader["Ukprn"], 0)).ToString();
            provider.UPIN = (int)CheckForDbNull(reader["UPIN"], 0);
            provider.Email = (string)CheckForDbNull(reader["Email"], string.Empty);
            provider.Website = (string)CheckForDbNull(reader["Website"], string.Empty);
            provider.Telephone = (string)CheckForDbNull(reader["Telephone"], string.Empty);
            provider.MarketingInformation = (string)CheckForDbNull(reader["MarketingInformation"], string.Empty);
            provider.NationalApprenticeshipProvider = (bool)CheckForDbNull(reader["NationalApprenticeshipProvider"], false);

            return provider;
        }

        public static List<Apprenticeship> GetApprenticeshipsByProviderId(int ProviderId, string connectionString, out string errorMessageGetApprenticeshipsByProviderId)
        {
            var apprenticeships = new List<Apprenticeship>();
            errorMessageGetApprenticeshipsByProviderId = string.Empty;

            using (var sqlConnection = new SqlConnection(connectionString))
            {
                using (var command = sqlConnection.CreateCommand())
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.CommandText = "dfc_GetApprenticeshipsByProviderId";

                    command.Parameters.Add(new SqlParameter("@ProviderId", SqlDbType.Int));
                    command.Parameters["@ProviderId"].Value = ProviderId;

                    try
                    {
                        //Open connection.
                        sqlConnection.Open();

                        using (SqlDataReader dataReader = command.ExecuteReader())
                        {
                            while (dataReader.Read())
                            {
                                var apprenticeship = ExtractApprenticeshipsFromDbReader(dataReader);
                                if (apprenticeship != null)
                                    apprenticeships.Add(apprenticeship);
                            }
                            // Close the SqlDataReader.
                            dataReader.Close();
                        }
                    }
                    catch (Exception ex)
                    {
                        errorMessageGetApprenticeshipsByProviderId = string.Format("Error Message: {0}" + Environment.NewLine + "Stack Trace: {1}", ex.Message, ex.StackTrace);
                    }
                    finally
                    {
                        sqlConnection.Close();
                    }
                }
            }

            return apprenticeships;
        }

        public static Apprenticeship ExtractApprenticeshipsFromDbReader(SqlDataReader reader)
        {
            Apprenticeship apprenticeship = new Apprenticeship();

            apprenticeship.ApprenticeshipId = (int)CheckForDbNull(reader["ApprenticeshipId"], 0);
            apprenticeship.TribalProviderId = (int)reader["ProviderId"];
            apprenticeship.StandardCode = (int?)CheckForDbNull(reader["StandardCode"], null);
            apprenticeship.Version = (int?)CheckForDbNull(reader["Version"], null);
            apprenticeship.FrameworkCode = (int?)CheckForDbNull(reader["FrameworkCode"], null);
            apprenticeship.ProgType = (int?)CheckForDbNull(reader["ProgType"], null);
            apprenticeship.PathwayCode = (int?)CheckForDbNull(reader["PathwayCode"], null);
            apprenticeship.MarketingInformation = (string)CheckForDbNull(reader["MarketingInformation"], string.Empty);
            apprenticeship.Url = (string)CheckForDbNull(reader["Url"], string.Empty);
            apprenticeship.ContactTelephone = (string)CheckForDbNull(reader["ContactTelephone"], string.Empty);
            apprenticeship.ContactEmail = (string)CheckForDbNull(reader["ContactEmail"], string.Empty);
            apprenticeship.ContactWebsite = (string)CheckForDbNull(reader["ContactWebsite"], string.Empty);

            return apprenticeship;
        }

        public static List<ApprenticeshipLocation> GetApprenticeshipLocationsByApprenticeshipId(int ApprenticeshipId, string connectionString, out string errorMessageGetApprenticeshipLocations)
        {
            var apprenticeshipLocations = new List<ApprenticeshipLocation>();
            errorMessageGetApprenticeshipLocations = string.Empty;

            using (var sqlConnection = new SqlConnection(connectionString))
            {
                using (var command = sqlConnection.CreateCommand())
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.CommandText = "dfc_GetApprenticeshipLocationsDetailsByApprenticeshipId";

                    command.Parameters.Add(new SqlParameter("@ApprenticeshipId", SqlDbType.Int));
                    command.Parameters["@ApprenticeshipId"].Value = ApprenticeshipId;

                    try
                    {
                        //Open connection.
                        sqlConnection.Open();

                        using (SqlDataReader dataReader = command.ExecuteReader())
                        {
                            while (dataReader.Read())
                            {
                                var apprenticeshipLocation = ExtractApprenticeshipLocationFromDbReader(dataReader);
                                if (apprenticeshipLocation != null)
                                    apprenticeshipLocations.Add(apprenticeshipLocation);
                            }
                            // Close the SqlDataReader.
                            dataReader.Close();
                        }
                    }
                    catch (Exception ex)
                    {
                        errorMessageGetApprenticeshipLocations = string.Format("Error Message: {0}" + Environment.NewLine + "Stack Trace: {1}", ex.Message, ex.StackTrace);
                    }
                    finally
                    {
                        sqlConnection.Close();
                    }
                }
            }

            return apprenticeshipLocations;
        }

        public static ApprenticeshipLocation ExtractApprenticeshipLocationFromDbReader(SqlDataReader reader)
        {
            ApprenticeshipLocation apprenticeshipLocation = new ApprenticeshipLocation();

            apprenticeshipLocation.ApprenticeshipLocationId = (int)CheckForDbNull(reader["ApprenticeshipLocationId"], 0);
            apprenticeshipLocation.LocationId = (int)CheckForDbNull(reader["LocationId"], 0);
            apprenticeshipLocation.Radius = (int)CheckForDbNull(reader["Radius"], 0);
            apprenticeshipLocation.Name = (string)CheckForDbNull(reader["LocationName"], "");

            return apprenticeshipLocation;
        }

        public static List<int> GetDeliveryModesByApprenticeshipLocationId(int ApprenticeshipLocationId, string connectionString, out string errorMessageGetDeliveryModes)
        {
            var deliveryModes = new List<int>();
            errorMessageGetDeliveryModes = string.Empty;

            using (var sqlConnection = new SqlConnection(connectionString))
            {
                using (var command = sqlConnection.CreateCommand())
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.CommandText = "dfc_GetDeliveryModesByApprenticeshipLocationId";

                    command.Parameters.Add(new SqlParameter("@ApprenticeshipLocationId", SqlDbType.Int));
                    command.Parameters["@ApprenticeshipLocationId"].Value = ApprenticeshipLocationId;

                    try
                    {
                        //Open connection.
                        sqlConnection.Open();

                        using (SqlDataReader dataReader = command.ExecuteReader())
                        {
                            while (dataReader.Read())
                            {
                                deliveryModes.Add(ExtractDeliveryModeFromDbReader(dataReader));
                            }
                            // Close the SqlDataReader.
                            dataReader.Close();
                        }
                    }
                    catch (Exception ex)
                    {
                        errorMessageGetDeliveryModes = string.Format("Error Message: {0}" + Environment.NewLine + "Stack Trace: {1}", ex.Message, ex.StackTrace);
                    }
                    finally
                    {
                        sqlConnection.Close();
                    }
                }
            }

            return deliveryModes;
        }

        public static int ExtractDeliveryModeFromDbReader(SqlDataReader reader)
        {
            return (int)CheckForDbNull(reader["DeliveryModeId"], 0);
        }

        public static Location GetLocationByLocationIdPerProvider(long LocationId, int ProviderId, string connectionString, out string errorMessageGetTribalLocation)
        {
            var location = new Location();
            bool hasData = false;
            errorMessageGetTribalLocation = string.Empty;

            using (var sqlConnection = new SqlConnection(connectionString))
            {
                using (var command = sqlConnection.CreateCommand())
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.CommandText = "dfc_GetLocationByLocationIdPerProvider";

                    command.Parameters.Add(new SqlParameter("@LocationId", SqlDbType.Int));
                    command.Parameters["@LocationId"].Value = LocationId;

                    command.Parameters.Add(new SqlParameter("@ProviderId", SqlDbType.Int));
                    command.Parameters["@ProviderId"].Value = ProviderId;

                    try
                    {
                        //Open connection.
                        sqlConnection.Open();

                        using (SqlDataReader dataReader = command.ExecuteReader())
                        {
                            while (dataReader.Read())
                            {
                                hasData = true;
                                location = ExtractTribalLocationFromDbReader(dataReader);
                            }
                            // Close the SqlDataReader.
                            dataReader.Close();
                        }
                    }
                    catch (Exception ex)
                    {
                        errorMessageGetTribalLocation = string.Format("Error Message: {0}" + Environment.NewLine + "Stack Trace: {1}", ex.Message, ex.StackTrace);
                    }
                    finally
                    {
                        sqlConnection.Close();
                    }
                }
            }

            location.LocationId = (int) LocationId;

            return hasData ? location : null;
        }

        public static Location ExtractTribalLocationFromDbReader(SqlDataReader reader)
        {
            Location location = new Location();

            location.LocationName = (string)CheckForDbNull(reader["LocationName"], string.Empty);
            location.AddressLine1 = (string)CheckForDbNull(reader["AddressLine1"], string.Empty);
            location.AddressLine2 = (string)CheckForDbNull(reader["AddressLine2"], string.Empty);
            location.Town = (string)CheckForDbNull(reader["Town"], string.Empty);
            location.County = (string)CheckForDbNull(reader["County"], string.Empty);
            location.Postcode = (string)CheckForDbNull(reader["Postcode"], string.Empty);
            location.Latitude = Convert.ToDecimal(CheckForDbNull(reader["Latitude"], 0));
            location.Longitude = Convert.ToDecimal(CheckForDbNull(reader["Longitude"], 0));
            location.Telephone = (string)CheckForDbNull(reader["Telephone"], string.Empty);
            location.Email = (string)CheckForDbNull(reader["Email"], string.Empty);
            location.Website = (string)CheckForDbNull(reader["Website"], string.Empty);

            return location;
        }

        public static ONSPDRegionSubregion GetRegionSubRegionByPostcode(string Postcode, string connectionString, out string errorMessageGetRegionSubRegion)
        {
            var onspdRegionSubregion = new ONSPDRegionSubregion();
            errorMessageGetRegionSubRegion = string.Empty;

            using (var sqlConnection = new SqlConnection(connectionString))
            {
                try
                {
                    onspdRegionSubregion = GetRegionSubRegionByPostcodeSql(Postcode, sqlConnection);
                    if (string.IsNullOrEmpty(onspdRegionSubregion.Region))
                    {
                        onspdRegionSubregion = GetRegionSubRegionByPostcodeSql(Postcode.Replace(" ", ""), sqlConnection);
                    }
                }
                catch (Exception ex)
                {
                    errorMessageGetRegionSubRegion = string.Format("Error Message: {0}" + Environment.NewLine + "Stack Trace: {1}", ex.Message, ex.StackTrace);
                    sqlConnection.Close();
                }

            }

            onspdRegionSubregion.Postcode = Postcode;

            return onspdRegionSubregion;
        }

        private static ONSPDRegionSubregion GetRegionSubRegionByPostcodeSql(string Postcode, SqlConnection sqlConnection)
        {

            var onspdRegionSubregion = new ONSPDRegionSubregion();

            using (var command = sqlConnection.CreateCommand())
            {
                command.CommandType = CommandType.StoredProcedure;
                command.CommandText = "dfc_GetRegionSubRegionByPostcode";

                command.Parameters.Add(new SqlParameter("@Postcode", SqlDbType.NVarChar, 50));
                command.Parameters["@Postcode"].Value = Postcode;

                //Open connection.
                sqlConnection.Open();

                using (SqlDataReader dataReader = command.ExecuteReader())
                {
                    while (dataReader.Read())
                    {
                        onspdRegionSubregion = ExtractONSPDRegionSubregionFromDbReader(dataReader);
                    }
                    // Close the SqlDataReader.
                    dataReader.Close();
                }
            }
            sqlConnection.Close();

            return onspdRegionSubregion;
        }

        public static ONSPDRegionSubregion ExtractONSPDRegionSubregionFromDbReader(SqlDataReader reader)
        {
            ONSPDRegionSubregion onspdRegionSubregion = new ONSPDRegionSubregion();

            onspdRegionSubregion.Region = (string)CheckForDbNull(reader["Region"], string.Empty);
            onspdRegionSubregion.SubRegion = (string)CheckForDbNull(reader["SubRegion"], string.Empty);

            return onspdRegionSubregion;
        }

        public static List<ApprenticeshipQaCompliance> GetApprenticeshipQaCompliance(ILogger logger,
            int apprenticeshipId, string connectionString)
        {
            bool hasData = false;
            DataSet ds = new DataSet();
            using (var sqlConnection = new SqlConnection(connectionString))
            {
                using (var command = sqlConnection.CreateCommand())
                {
                    SqlConnection con = new SqlConnection(connectionString);
                    command.CommandType = CommandType.StoredProcedure;
                    command.CommandText = "dfc_GetApprenticeshipQaDetailslViaApprenticeshipId";

                    command.Parameters.Add(new SqlParameter("@apprenticeshipId", SqlDbType.Int));
                    command.Parameters["@apprenticeshipId"].Value = apprenticeshipId;
                    

                    try
                    {
                        SqlDataAdapter da = new SqlDataAdapter();
                        da = new SqlDataAdapter(command);
                        da.Fill(ds);
                        con.Close();
                        hasData = true;
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(string.Format("Error Message: {0}" + Environment.NewLine + "Stack Trace: {1}", ex.Message, ex.StackTrace));
                    }
                    finally
                    {
                        sqlConnection.Close();
                    }
                }
            }

            return hasData ? ExtractApprenticeshipQaComplianceFromDataSet(ds) : null;
        }

        public static List<ApprenticeshipQaStyle> GetApprenticeshipQaStyle(ILogger logger,
            int apprenticeshipId, string connectionString)
        {
            bool hasData = false;
            DataSet ds = new DataSet();
            using (var sqlConnection = new SqlConnection(connectionString))
            {
                using (var command = sqlConnection.CreateCommand())
                {
                    SqlConnection con = new SqlConnection(connectionString);
                    command.CommandType = CommandType.StoredProcedure;
                    command.CommandText = "dfc_GetApprenticeshipQaStyleDetails";

                    command.Parameters.Add(new SqlParameter("@apprenticeshipId", SqlDbType.Int));
                    command.Parameters["@apprenticeshipId"].Value = apprenticeshipId;


                    try
                    {
                        SqlDataAdapter da = new SqlDataAdapter();
                        da = new SqlDataAdapter(command);
                        da.Fill(ds);
                        con.Close();
                        hasData = true;
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(string.Format("Error Message: {0}" + Environment.NewLine + "Stack Trace: {1}", ex.Message, ex.StackTrace));
                    }
                    finally
                    {
                        sqlConnection.Close();
                    }
                }
            }

            return hasData ? ExtractApprenticeshipQaStyleFromDataSet(ds) : null;
        }

        private static List<ApprenticeshipQaCompliance> ExtractApprenticeshipQaComplianceFromDataSet(DataSet dataSet)
        {
            var qaComplianceList = new List<ApprenticeshipQaCompliance>();

            var qaComplianceRows = dataSet.Tables[0]?.Rows;

            foreach (DataRow dataRow in qaComplianceRows)
            {
                var qaCompliance = new ApprenticeshipQaCompliance
                {
                    ApprenticeshipId = (int)dataRow["ApprenticeshipId"],
                    ApprenticeshipQaComplianceId = (int)dataRow["ApprenticeshipQAComplianceId"],
                    CreatedByUserEmail = (string)CheckForDbNull(dataRow["CreatedByUserEmail"],
                        string.Empty),
                    CreatedDateTimeUtc = dataRow["CreatedDateTimeUtc"].ToString(),
                    DetailsOfComplianceFailure = (string)CheckForDbNull(dataRow["DetailsOfComplianceFailure"],
                        string.Empty),
                    DetailsOfUnverifiableClaim = (string)CheckForDbNull(dataRow["DetailsOfUnverifiableClaim"],
                        string.Empty),
                    Passed = (bool)CheckForDbNull(dataRow["Passed"],
                        false),
                    TextQAd = (string)CheckForDbNull(dataRow["TextQAd"],
                        string.Empty),
                    FailureReasons = ExtractFailureReason(dataSet.Tables[1]?.Rows,
                        "ApprenticeshipQAComplianceId", "QAComplianceFailureReason", 
                        (int)dataRow["ApprenticeshipQAComplianceId"])
                };
                qaComplianceList.Add(qaCompliance);
            }

            return qaComplianceList;
        }

        private static List<ApprenticeshipQaStyle> ExtractApprenticeshipQaStyleFromDataSet(DataSet dataSet)
        {
            var qaComplianceList = new List<ApprenticeshipQaStyle>();

            var qaComplianceRows = dataSet.Tables[0]?.Rows;

            foreach (DataRow dataRow in qaComplianceRows)
            {
                var qaCompliance = new ApprenticeshipQaStyle
                {
                    ApprenticeshipId = (int)dataRow["ApprenticeshipId"],
                    ApprenticeshipQaStyleId = (int)dataRow["ApprenticeshipQAStyleId"],
                    CreatedByUserEmail = (string)CheckForDbNull(dataRow["CreatedByUserEmail"],
                        string.Empty),
                    Passed = (bool)CheckForDbNull(dataRow["Passed"],
                        false),
                    TextQAd = (string)CheckForDbNull(dataRow["TextQAd"],
                        string.Empty),
                    CreatedDateTimeUtc = dataRow["CreatedDateTimeUtc"].ToString(),
                    DetailsOfQa = (string)CheckForDbNull(dataRow["DetailsOfQA"], string.Empty),
                    FailureReasons = ExtractFailureReason(dataSet.Tables[1]?.Rows,
                        "ApprenticeshipQAStyleId", "QAStyleFailureReason",
                        (int)dataRow["ApprenticeshipQAStyleId"])
                };
                qaComplianceList.Add(qaCompliance);
            }

            return qaComplianceList;
        }


        private static List<string> ExtractFailureReason(DataRowCollection rows, string columnId, string columnData, int id)
        {
            var failures = new List<string>();
            foreach (DataRow dataRow in rows)
            {
                if ((int)dataRow[columnId] == id)
                {
                    failures.Add((string)CheckForDbNull(dataRow[columnData], ""));
                }
            }
            return failures;
        }

        // Auditing NOT USED yet

        public static void CourseTransferAdd(string connectionString,
                                                DateTime startTransferDateTime,
                                                int transferMethod,
                                                int deploymentEnvironment,
                                                string createdById,
                                                string createdByName,
                                                int? ukprn,
                                                out string errorMessageCourseTransferAdd,
                                                out int courseTransferId)
        {
            var ukprnList = new List<int>();
            courseTransferId = 0;
            errorMessageCourseTransferAdd = string.Empty;

            using (var sqlConnection = new SqlConnection(connectionString))
            {
                using (var command = sqlConnection.CreateCommand())
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.CommandText = "dfc_CourseTransferAdd";

                    command.Parameters.Add(new SqlParameter("@StartTransferDateTime", SqlDbType.DateTime));
                    command.Parameters["@StartTransferDateTime"].Value = startTransferDateTime;

                    command.Parameters.Add(new SqlParameter("@TransferMethod", SqlDbType.Int));
                    command.Parameters["@TransferMethod"].Value = transferMethod;

                    command.Parameters.Add(new SqlParameter("@DeploymentEnvironment", SqlDbType.Int));
                    command.Parameters["@DeploymentEnvironment"].Value = deploymentEnvironment;

                    command.Parameters.Add(new SqlParameter("@CreatedById", SqlDbType.NVarChar, 128));
                    command.Parameters["@CreatedById"].Value = createdById;

                    command.Parameters.Add(new SqlParameter("@CreatedByName", SqlDbType.NVarChar, 255));
                    command.Parameters["@CreatedByName"].Value = createdByName;

                    command.Parameters.Add(new SqlParameter("@Ukprn", SqlDbType.Int));

                    if (ukprn == null) command.Parameters["@Ukprn"].Value = DBNull.Value;
                    else command.Parameters["@Ukprn"].Value = ukprn;
                    //command.Parameters["@Ukprn"].Value = ukprn == null ? DBNull.Value : ukprn;
                    //command.Parameters["@Ukprn"].Value = ukprn ?? DBNull.Value;

                    command.Parameters.Add(new SqlParameter("@CourseTransferId", SqlDbType.Int));
                    command.Parameters["@CourseTransferId"].Direction = ParameterDirection.Output;

                    try
                    {
                        //Open connection.
                        sqlConnection.Open();
                        command.ExecuteNonQuery();
                        // Get the CourseTransferId (Batch Number)
                        courseTransferId = (int)CheckForDbNull(command.Parameters["@CourseTransferId"].Value, -1);
                    }
                    catch (Exception ex)
                    {
                        errorMessageCourseTransferAdd = string.Format("Error Message: {0}" + Environment.NewLine + "Stack Trace: {1}", ex.Message, ex.StackTrace);
                    }
                    finally
                    {
                        sqlConnection.Close();
                    }
                }
            }
        }

        public static void CourseTransferUpdate(string connectionString,
                                                int courseTransferId,
                                                int countProvidersToBeMigrated,
                                                int countProvidersMigrated,
                                                int countProvidersNotMigrated,
                                                int countAllCoursesToBeMigrated,
                                                int countCoursesGoodToBeMigrated,
                                                int countCoursesNotGoodToBeMigrated,
                                                int countCoursesGoodToBeMigratedLive,
                                                int countCoursesGoodToBeMigratedPending,
                                                int countAllCoursesLARSless,
                                                int countAllCoursesMigrated,
                                                int countAllCoursesNotMigrated,
                                                DateTime completeTransferDateTime,
                                                string timeTaken,
                                                string bulkUploadFileName,
                                                string adminReportFileName,
                                                string transferNote,
                                                out string errorMessageCourseTransferUpdate)
        {
            var ukprnList = new List<int>();
            errorMessageCourseTransferUpdate = string.Empty;

            using (var sqlConnection = new SqlConnection(connectionString))
            {
                using (var command = sqlConnection.CreateCommand())
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.CommandText = "dfc_CourseTransferUpdate";

                    command.Parameters.Add(new SqlParameter("@CourseTransferId", SqlDbType.Int));
                    command.Parameters["@CourseTransferId"].Value = courseTransferId;

                    command.Parameters.Add(new SqlParameter("@CountProvidersToBeMigrated", SqlDbType.Int));
                    command.Parameters["@CountProvidersToBeMigrated"].Value = countProvidersToBeMigrated;

                    command.Parameters.Add(new SqlParameter("@CountProvidersMigrated", SqlDbType.Int));
                    command.Parameters["@CountProvidersMigrated"].Value = countProvidersMigrated;

                    command.Parameters.Add(new SqlParameter("@CountProvidersNotMigrated", SqlDbType.Int));
                    command.Parameters["@CountProvidersNotMigrated"].Value = countProvidersNotMigrated;

                    command.Parameters.Add(new SqlParameter("@CountAllCoursesToBeMigrated", SqlDbType.Int));
                    command.Parameters["@CountAllCoursesToBeMigrated"].Value = countAllCoursesToBeMigrated;

                    command.Parameters.Add(new SqlParameter("@CountCoursesGoodToBeMigrated", SqlDbType.Int));
                    command.Parameters["@CountCoursesGoodToBeMigrated"].Value = countCoursesGoodToBeMigrated;

                    command.Parameters.Add(new SqlParameter("@CountCoursesNotGoodToBeMigrated", SqlDbType.Int));
                    command.Parameters["@CountCoursesNotGoodToBeMigrated"].Value = countCoursesNotGoodToBeMigrated;

                    command.Parameters.Add(new SqlParameter("@CountCoursesGoodToBeMigratedLive", SqlDbType.Int));
                    command.Parameters["@CountCoursesGoodToBeMigratedLive"].Value = countCoursesGoodToBeMigratedLive;

                    command.Parameters.Add(new SqlParameter("@CountCoursesGoodToBeMigratedPending", SqlDbType.Int));
                    command.Parameters["@CountCoursesGoodToBeMigratedPending"].Value = countCoursesGoodToBeMigratedPending;

                    command.Parameters.Add(new SqlParameter("@CountAllCoursesLARSless", SqlDbType.Int));
                    command.Parameters["@CountAllCoursesLARSless"].Value = countAllCoursesLARSless;

                    command.Parameters.Add(new SqlParameter("@CountAllCoursesMigrated", SqlDbType.Int));
                    command.Parameters["@CountAllCoursesMigrated"].Value = countAllCoursesMigrated;

                    command.Parameters.Add(new SqlParameter("@CountAllCoursesNotMigrated", SqlDbType.Int));
                    command.Parameters["@CountAllCoursesNotMigrated"].Value = countAllCoursesNotMigrated;

                    command.Parameters.Add(new SqlParameter("@CompleteTransferDateTime", SqlDbType.DateTime));
                    command.Parameters["@CompleteTransferDateTime"].Value = completeTransferDateTime;

                    command.Parameters.Add(new SqlParameter("@TimeTaken", SqlDbType.VarChar, 50));
                    command.Parameters["@TimeTaken"].Value = timeTaken;

                    command.Parameters.Add(new SqlParameter("@BulkUploadFileName", SqlDbType.NVarChar, 1000));
                    command.Parameters["@BulkUploadFileName"].Value = bulkUploadFileName;

                    command.Parameters.Add(new SqlParameter("@AdminReportFileName", SqlDbType.VarChar, 255));
                    command.Parameters["@AdminReportFileName"].Value = adminReportFileName;

                    command.Parameters.Add(new SqlParameter("@TransferNote", SqlDbType.NVarChar, -1));
                    command.Parameters["@TransferNote"].Value = transferNote;

                    try
                    {
                        //Open connection.
                        sqlConnection.Open();
                        command.ExecuteNonQuery();
                    }
                    catch (Exception ex)
                    {
                        errorMessageCourseTransferUpdate = string.Format("Error Message: {0}" + Environment.NewLine + "Stack Trace: {1}", ex.Message, ex.StackTrace);
                    }
                    finally
                    {
                        sqlConnection.Close();
                    }
                }
            }
        }

        public static void CourseTransferCourseAuditAdd(string connectionString,
                                                           int courseTransferId,
                                                           int ukprn,
                                                           int courseId,
                                                           string lars,
                                                           int courseRecordStatus,
                                                           int courseRuns,
                                                           int courseRunsLive,
                                                           int courseRunsPending,
                                                           int courseRunsReadyToGoLive,
                                                           int courseRunsLARSless,
                                                           int migrationSuccess,
                                                           string courseMigrationNote,
                                                           out string errorMessageCourseAuditAdd)
        {
            errorMessageCourseAuditAdd = string.Empty;

            using (var sqlConnection = new SqlConnection(connectionString))
            {
                using (var command = sqlConnection.CreateCommand())
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.CommandText = "dfc_CourseTransferCourseAuditAdd";

                    command.Parameters.Add(new SqlParameter("@CourseTransferId", SqlDbType.Int));
                    command.Parameters["@CourseTransferId"].Value = courseTransferId;

                    command.Parameters.Add(new SqlParameter("@Ukprn", SqlDbType.Int));
                    command.Parameters["@Ukprn"].Value = ukprn;

                    command.Parameters.Add(new SqlParameter("@CourseId", SqlDbType.Int));
                    command.Parameters["@CourseId"].Value = courseId;

                    command.Parameters.Add(new SqlParameter("@LARS", SqlDbType.VarChar, 10));
                    command.Parameters["@LARS"].Value = lars ?? string.Empty;

                    command.Parameters.Add(new SqlParameter("@CourseRecordStatus", SqlDbType.Int));
                    command.Parameters["@CourseRecordStatus"].Value = courseRecordStatus;

                    command.Parameters.Add(new SqlParameter("@CourseRuns", SqlDbType.Int));
                    command.Parameters["@CourseRuns"].Value = courseRuns;

                    command.Parameters.Add(new SqlParameter("@CourseRunsLive", SqlDbType.Int));
                    command.Parameters["@CourseRunsLive"].Value = courseRunsLive;

                    command.Parameters.Add(new SqlParameter("@CourseRunsPending", SqlDbType.Int));
                    command.Parameters["@CourseRunsPending"].Value = courseRunsPending;

                    command.Parameters.Add(new SqlParameter("@CourseRunsReadyToGoLive", SqlDbType.Int));
                    command.Parameters["@CourseRunsReadyToGoLive"].Value = courseRunsReadyToGoLive;

                    command.Parameters.Add(new SqlParameter("@CourseRunsLARSless", SqlDbType.Int));
                    command.Parameters["@CourseRunsLARSless"].Value = courseRunsLARSless;

                    command.Parameters.Add(new SqlParameter("@MigrationSuccess", SqlDbType.Int));
                    command.Parameters["@MigrationSuccess"].Value = migrationSuccess;

                    command.Parameters.Add(new SqlParameter("@CourseMigrationNote", SqlDbType.NVarChar, -1));
                    command.Parameters["@CourseMigrationNote"].Value = courseMigrationNote;


                    try
                    {
                        //Open connection.
                        sqlConnection.Open();
                        command.ExecuteNonQuery();
                    }
                    catch (Exception ex)
                    {
                        errorMessageCourseAuditAdd = string.Format("Error Message: {0}" + Environment.NewLine + "Stack Trace: {1}", ex.Message, ex.StackTrace);
                    }
                    finally
                    {
                        sqlConnection.Close();
                    }
                }
            }
        }

        public static void CourseTransferProviderAuditAdd(string connectionString,
                                                           int courseTransferId,
                                                           int ukprn,
                                                           int coursesToBeMigrated,
                                                           int coursesGoodToBeMigrated,
                                                           int coursesGoodToBeMigratedPending,
                                                           int coursesGoodToBeMigratedLive,
                                                           int coursesNotGoodToBeMigrated,
                                                           int coursesLARSless,
                                                           int migrationSuccesses,
                                                           int migrationFailures,
                                                           string providerReportFileName,
                                                           string timeTaken,
                                                           string migrationNote,
                                                           out string errorMessageProviderAuditAdd)
        {
            errorMessageProviderAuditAdd = string.Empty;

            using (var sqlConnection = new SqlConnection(connectionString))
            {
                using (var command = sqlConnection.CreateCommand())
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.CommandText = "dfc_CourseTransferProviderAuditAdd";

                    command.Parameters.Add(new SqlParameter("@CourseTransferId", SqlDbType.Int));
                    command.Parameters["@CourseTransferId"].Value = courseTransferId;

                    command.Parameters.Add(new SqlParameter("@Ukprn", SqlDbType.Int));
                    command.Parameters["@Ukprn"].Value = ukprn;

                    command.Parameters.Add(new SqlParameter("@CoursesToBeMigrated", SqlDbType.Int));
                    command.Parameters["@CoursesToBeMigrated"].Value = coursesToBeMigrated;

                    command.Parameters.Add(new SqlParameter("@CoursesGoodToBeMigrated", SqlDbType.Int));
                    command.Parameters["@CoursesGoodToBeMigrated"].Value = coursesGoodToBeMigrated;

                    command.Parameters.Add(new SqlParameter("@CoursesGoodToBeMigratedPending", SqlDbType.Int));
                    command.Parameters["@CoursesGoodToBeMigratedPending"].Value = coursesGoodToBeMigratedPending;

                    command.Parameters.Add(new SqlParameter("@CoursesGoodToBeMigratedLive", SqlDbType.Int));
                    command.Parameters["@CoursesGoodToBeMigratedLive"].Value = coursesGoodToBeMigratedLive;

                    command.Parameters.Add(new SqlParameter("@CoursesNotGoodToBeMigrated", SqlDbType.Int));
                    command.Parameters["@CoursesNotGoodToBeMigrated"].Value = coursesNotGoodToBeMigrated;

                    command.Parameters.Add(new SqlParameter("@CoursesLARSless", SqlDbType.Int));
                    command.Parameters["@CoursesLARSless"].Value = coursesLARSless;

                    command.Parameters.Add(new SqlParameter("@MigrationSuccesses", SqlDbType.Int));
                    command.Parameters["@MigrationSuccesses"].Value = migrationSuccesses;

                    command.Parameters.Add(new SqlParameter("@MigrationFailures", SqlDbType.Int));
                    command.Parameters["@MigrationFailures"].Value = migrationFailures;

                    command.Parameters.Add(new SqlParameter("@ProviderReportFileName", SqlDbType.VarChar, 255));
                    command.Parameters["@ProviderReportFileName"].Value = providerReportFileName;

                    command.Parameters.Add(new SqlParameter("@TimeTaken", SqlDbType.VarChar, 50));
                    command.Parameters["@TimeTaken"].Value = timeTaken;

                    command.Parameters.Add(new SqlParameter("@MigrationNote", SqlDbType.NVarChar, -1));
                    command.Parameters["@MigrationNote"].Value = migrationNote;



                    try
                    {
                        //Open connection.
                        sqlConnection.Open();
                        command.ExecuteNonQuery();
                    }
                    catch (Exception ex)
                    {
                        errorMessageProviderAuditAdd = string.Format("Error Message: {0}" + Environment.NewLine + "Stack Trace: {1}", ex.Message, ex.StackTrace);
                    }
                    finally
                    {
                        sqlConnection.Close();
                    }
                }
            }
        }

        public static List<TribalCourse> GetCoursesByProviderUKPRN(int ProviderUKPRN, string connectionString, out string ProviderName, out bool AdvancedLearnerLoan, out string errorMessageGetCourses)
        {
            var tribalCourses = new List<TribalCourse>();
            ProviderName = string.Empty;
            AdvancedLearnerLoan = false;
            errorMessageGetCourses = string.Empty;

            using (var sqlConnection = new SqlConnection(connectionString))
            {
                using (var command = sqlConnection.CreateCommand())
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.CommandText = "dfc_GetCoursesByProviderUKPRN";

                    command.Parameters.Add(new SqlParameter("@ProviderUKPRN", SqlDbType.Int));
                    command.Parameters["@ProviderUKPRN"].Value = ProviderUKPRN;

                    command.Parameters.Add(new SqlParameter("@ProviderName", SqlDbType.NVarChar, 200));
                    command.Parameters["@ProviderName"].Direction = ParameterDirection.Output;

                    command.Parameters.Add(new SqlParameter("@AdvancedLearnerLoan", SqlDbType.Bit));
                    command.Parameters["@AdvancedLearnerLoan"].Direction = ParameterDirection.Output;

                    try
                    {
                        //Open connection.
                        sqlConnection.Open();

                        using (SqlDataReader dataReader = command.ExecuteReader())
                        {
                            while (dataReader.Read())
                            {
                                TribalCourse tribalCourse = ExtractCourseFromDbReader(dataReader);
                                if (tribalCourse != null)
                                    tribalCourses.Add(tribalCourse);
                            }
                            // Close the SqlDataReader.
                            dataReader.Close();
                        }

                        // Get the Provider Name
                        ProviderName = (string)CheckForDbNull(command.Parameters["@ProviderName"].Value, string.Empty);
                        // Get the AdvancedLearnerLoan
                        AdvancedLearnerLoan = (bool)CheckForDbNull(command.Parameters["@AdvancedLearnerLoan"].Value, false);


                    }
                    catch (Exception ex)
                    {
                        errorMessageGetCourses = string.Format("Error Message: {0}" + Environment.NewLine + "Stack Trace: {1}", ex.Message, ex.StackTrace);
                    }
                    finally
                    {
                        sqlConnection.Close();
                    }
                }
            }

            return tribalCourses;
        }

        public static TribalCourse ExtractCourseFromDbReader(SqlDataReader reader)
        {
            TribalCourse tribalCourse = new TribalCourse();

            tribalCourse.Ukprn = (int)CheckForDbNull(reader["Ukprn"], 0);
            tribalCourse.CourseId = (int)CheckForDbNull(reader["CourseId"], 0);
            tribalCourse.CourseTitle = (string)CheckForDbNull(reader["CourseTitle"], string.Empty);
            tribalCourse.LearningAimRefId = (string)CheckForDbNull(reader["LearningAimRefId"], string.Empty);
            tribalCourse.QualificationLevelId = (int)CheckForDbNull(reader["QualificationLevelId"], 0);
            tribalCourse.LearningAimAwardOrgCode = (string)CheckForDbNull(reader["LearningAimAwardOrgCode"], string.Empty);
            tribalCourse.Qualification = (string)CheckForDbNull(reader["Qualification"], string.Empty);
            tribalCourse.CourseSummary = (string)CheckForDbNull(reader["CourseSummary"], string.Empty);
            tribalCourse.EntryRequirements = (string)CheckForDbNull(reader["EntryRequirements"], string.Empty);
            tribalCourse.EquipmentRequired = (string)CheckForDbNull(reader["EquipmentRequired"], string.Empty);
            tribalCourse.AssessmentMethod = (string)CheckForDbNull(reader["AssessmentMethod"], string.Empty);
            ////
            //tribalCourse.AdvancedLearnerLoan = // TODO:
            return tribalCourse;
        }

        public static List<TribalCourseRun> GetCourseInstancesByCourseId(int CourseId, string connectionString, out string errorMessageGetCourseRuns)
        {
            errorMessageGetCourseRuns = string.Empty;
            var tribalCourseRuns = new List<TribalCourseRun>();

            using (var sqlConnection = new SqlConnection(connectionString))
            {
                using (var command = sqlConnection.CreateCommand())
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.CommandText = "dfc_GetCourseInstancesByCourseId";

                    command.Parameters.Add(new SqlParameter("@CourseId", SqlDbType.Int));
                    command.Parameters["@CourseId"].Value = CourseId;

                    try
                    {
                        //Open connection.
                        sqlConnection.Open();

                        using (SqlDataReader dataReader = command.ExecuteReader())
                        {
                            while (dataReader.Read())
                            {
                                TribalCourseRun tribalCourseRun = ExtractCourseRunFromDbReader(dataReader);
                                if (tribalCourseRun != null)
                                    tribalCourseRuns.Add(tribalCourseRun);
                            }
                            // Close the SqlDataReader.
                            dataReader.Close();
                        }
                    }
                    catch (Exception ex)
                    {
                        errorMessageGetCourseRuns = string.Format("Error Message: {0}" + Environment.NewLine + "Stack Trace: {1}", ex.Message, ex.StackTrace);
                    }
                    finally
                    {
                        sqlConnection.Close();
                    }
                }
            }

            return tribalCourseRuns;
        }

        public static TribalCourseRun ExtractCourseRunFromDbReader(SqlDataReader reader)
        {
            TribalCourseRun tribalCourseRun = new TribalCourseRun();

            tribalCourseRun.CourseId = (int)CheckForDbNull(reader["CourseId"], 0);
            tribalCourseRun.VenueId = (int?)CheckForDbNull(reader["VenueId"], null);
            tribalCourseRun.CourseInstanceId = (int)CheckForDbNull(reader["CourseInstanceId"], 0);
            tribalCourseRun.ProviderOwnCourseInstanceRef = (string)CheckForDbNull(reader["ProviderOwnCourseInstanceRef"], string.Empty);
            tribalCourseRun.AttendanceType = (AttendanceType)CheckForDbNull(reader["AttendanceTypeId"], AttendanceType.Undefined);
            tribalCourseRun.StartDateDescription = (string)CheckForDbNull(reader["StartDateDescription"], string.Empty);
            tribalCourseRun.StartDate = (DateTime?)CheckForDbNull(reader["StartDate"], null);
            tribalCourseRun.Url = (string)CheckForDbNull(reader["Url"], string.Empty);
            tribalCourseRun.Price = (decimal?)CheckForDbNull(reader["Price"], null);
            tribalCourseRun.PriceAsText = (string)CheckForDbNull(reader["PriceAsText"], string.Empty);
            tribalCourseRun.DurationUnit = (TribalDurationUnit)CheckForDbNull(reader["DurationUnitId"], TribalDurationUnit.Undefined);
            tribalCourseRun.DurationValue = (int)CheckForDbNull(reader["DurationUnit"], 0);
            tribalCourseRun.StudyMode = (TribalStudyMode)CheckForDbNull(reader["StudyModeId"], TribalStudyMode.Undefined);
            tribalCourseRun.AttendancePattern = (TribalAttendancePattern)CheckForDbNull(reader["AttendancePatternId"], TribalAttendancePattern.Undefined);

            return tribalCourseRun;
        }

        public static object CheckForDbNull(object valueToCheck, object replacementValue)
        {
            return valueToCheck == DBNull.Value ? replacementValue : valueToCheck;
        }
    }
}
