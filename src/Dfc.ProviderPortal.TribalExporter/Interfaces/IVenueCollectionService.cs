﻿using Dfc.CourseDirectory.Models.Models.Venues;
using System;
using System.Threading.Tasks;

namespace Dfc.ProviderPortal.TribalExporter.Interfaces
{
    public interface IVenueCollectionService
    {
        Task<string> GetAllVenuesAsJsonForUkprnAsync(int ukprn);
        Task<bool> HasBeenAnUpdatedSinceAsync(int ukprn, DateTime date);
        Task<Venue> GetDocumentByVenueId(int venueId);
        Task<Venue> GetDocumentByLocationId(int locationId, int ukprn);
    }
}