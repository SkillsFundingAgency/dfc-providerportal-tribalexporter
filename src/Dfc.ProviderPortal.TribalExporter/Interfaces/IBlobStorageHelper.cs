﻿using Microsoft.WindowsAzure.Storage.Blob;
using System;
using System.Collections.Generic;
using System.Text;

namespace Dfc.ProviderPortal.TribalExporter.Interfaces
{
    public interface IBlobStorageHelper
    {
        CloudBlobContainer GetBlobContainer(string containerName);
    }
}
