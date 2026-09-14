using System;
using System.Collections.Generic;
using Amazon;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

// Cloud Readiness Fix (cr-dotnet-0123):
// Helper that retrieves secrets from AWS Secrets Manager at runtime,
// replacing hardcoded credentials in source code and configuration files.
// Secrets are encrypted at rest, support automatic rotation, and can be
// updated without redeployment.

namespace QLDSV_HTC.BLL
{
    /// <summary>
    /// Retrieves secrets from AWS Secrets Manager.
    /// Replaces hardcoded credentials (passwords, API keys, tokens) so that
    /// sensitive values are never stored in source code or config files.
    /// </summary>
    public static class AwsSecretsManagerHelper
    {
        private static readonly string AwsRegion =
            Environment.GetEnvironmentVariable("AWS_REGION") ?? "ap-southeast-1";

        // Simple in-process cache to avoid repeated API calls within the
        // same application lifetime.
        private static readonly Dictionary<string, string> _cache =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Returns the raw secret string for the given secret name.
        /// </summary>
        public static string GetSecret(string secretName)
        {
            if (_cache.TryGetValue(secretName, out string cached))
            {
                return cached;
            }

            var region = RegionEndpoint.GetBySystemName(AwsRegion);
            using (var client = new AmazonSecretsManagerClient(region))
            {
                var request = new GetSecretValueRequest { SecretId = secretName };

                var response = client.GetSecretValueAsync(request)
                                     .GetAwaiter()
                                     .GetResult();

                string value = response.SecretString
                               ?? Convert.ToBase64String(
                                      response.SecretBinary.ToArray());

                _cache[secretName] = value;
                return value;
            }
        }

        /// <summary>
        /// Returns a specific key from a JSON-formatted secret.
        /// </summary>
        public static string GetSecretValue(string secretName, string key)
        {
            string json = GetSecret(secretName);
            var obj = JObject.Parse(json);
            return obj[key]?.ToString()
                   ?? throw new KeyNotFoundException(
                          $"Key '{key}' not found in secret '{secretName}'.");
        }
    }
}
