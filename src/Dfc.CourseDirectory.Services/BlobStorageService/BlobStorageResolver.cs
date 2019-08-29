using Dfc.CourseDirectory.Services.Interfaces.BlobStorageService;

namespace Dfc.CourseDirectory.Services.BlobStorageService
{
    public delegate IBlobStorageService BlobStorageServiceResolver(string key);
    
    
}
