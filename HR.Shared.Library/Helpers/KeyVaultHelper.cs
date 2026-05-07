using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HR.Shared.Library.Helpers
{
    public interface IKeyVaultHelper
    {
        Task<string> GetSecretValueAsync(string secretName);
    }
    public class KeyVaultHelper : IKeyVaultHelper
    {
        private readonly SecretClient _client;

        public KeyVaultHelper(string vaultUri)
        {
            // DefaultAzureCredential local development (VS/CLI) 
            // aur Azure (Managed Identity) dono ke liye kaam karta hai.
            _client = new SecretClient(new Uri(vaultUri), new DefaultAzureCredential());
        }

        public async Task<string> GetSecretValueAsync(string secretName)
        {
            var secret = await _client.GetSecretAsync(secretName);
            return secret.Value.Value;
        }
    }
}
