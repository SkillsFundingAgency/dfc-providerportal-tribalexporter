using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Dfc.ProviderPortal.ApprenticeshipMigration.Interfaces
{
    public interface IApprenticeshipMigration
    {
        Task RunApprenticeShipMigration(ILogger log);
    }
}
