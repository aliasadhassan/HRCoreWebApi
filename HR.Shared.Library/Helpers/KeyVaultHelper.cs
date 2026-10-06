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

        /// <summary>Secret na ho (404) to null — optional / per-service override secrets ke liye.</summary>
        Task<string?> TryGetSecretValueAsync(string secretName);

        /// <summary>Service ka apna DB secret (RLS app role) ho to woh, warna shared SupabaseConnectionString.</summary>
        Task<string> GetDbConnectionStringAsync(string serviceSecretName);
    }
    public class KeyVaultHelper : IKeyVaultHelper
    {
        private readonly SecretClient _client;

        public KeyVaultHelper(string vaultUri)
        {
            // DefaultAzureCredential local development (VS/CLI) 
            // aur Azure (Managed Identity) dono ke liye kaam karta hai.
            //_client = new SecretClient(new Uri(vaultUri), new DefaultAzureCredential());

            // Ab hum AzureCredentialFactory ka use kar rahe hain, jo environment ke hisaab se credential choose karega.
            _client = new SecretClient(new Uri(vaultUri), AzureCredentialFactory.Create());
        }

        public async Task<string> GetSecretValueAsync(string secretName)
        {
            var secret = await _client.GetSecretAsync(secretName);
            return secret.Value.Value;
        }

        public async Task<string?> TryGetSecretValueAsync(string secretName)
        {
            try
            {
                return await GetSecretValueAsync(secretName);
            }
            catch (Azure.RequestFailedException ex) when (ex.Status == 404)
            {
                return null;
            }
        }

        public async Task<string> GetDbConnectionStringAsync(string serviceSecretName)
            => await TryGetSecretValueAsync(serviceSecretName)
               ?? await GetSecretValueAsync(KeyVaultSecrets.SharedDb);
    }
}
