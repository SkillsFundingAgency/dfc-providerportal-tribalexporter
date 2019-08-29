using Dfc.CourseDirectory.Models.Interfaces.Providers;

namespace Dfc.ProviderPortal.ApprenticeshipMigration.Models
{
    public class Provideralias : IProvideralias
    {
        public object ProviderAlias { get; set; }
        public object LastUpdated { get; set; }
    }
}
