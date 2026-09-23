using Azure.Core;
using Azure.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HR.Shared.Library.Helpers
{
    public static class AzureCredentialFactory
    {
        public static TokenCredential Create()
        {
            var isDev = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";

            return new DefaultAzureCredential(new DefaultAzureCredentialOptions
            {
                ExcludeManagedIdentityCredential = isDev,   // local: VS / Azure CLI
                ExcludeWorkloadIdentityCredential = isDev
            });                                             // Azure pe: Managed Identity normal kaam karegi
        }
    }
}
