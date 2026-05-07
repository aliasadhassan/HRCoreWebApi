using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HR.Shared.Library.Helpers
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddHRKeyVault(this IServiceCollection services, IConfiguration configuration)
        {
            var vaultUri = configuration["VaultUri"];
            if (!string.IsNullOrEmpty(vaultUri))
            {
                services.AddSingleton<IKeyVaultHelper>(new KeyVaultHelper(vaultUri));
            }
            return services;
        }
    }
}
