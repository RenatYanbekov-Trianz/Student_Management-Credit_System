using System;
using Amazon;
using Amazon.SimpleSystemsManagement;
using Amazon.SimpleSystemsManagement.Model;

// Cloud Readiness Fix (cr-dotnet-0010):
// Helper that retrieves configuration values from AWS Systems Manager
// Parameter Store at runtime, replacing Web.config / App.config
// build-time transformations.  The AWS region is resolved from the
// environment variable AWS_REGION (set by ECS / EC2 instance metadata)
// or defaults to ap-southeast-1.

namespace QLDSV_HTC.DAL
{
    /// <summary>
    /// Retrieves configuration parameters from AWS Systems Manager
    /// Parameter Store.  Replaces Web.config / App.config build-time
    /// transformations so that configuration is runtime-injectable and
    /// never baked into deployment artefacts.
    /// </summary>
    internal static class AwsParameterStoreHelper
    {
        private static readonly string AwsRegion =
            Environment.GetEnvironmentVariable("AWS_REGION") ?? "ap-southeast-1";

        /// <summary>
        /// Fetches a plain-text or SecureString parameter value from SSM.
        /// </summary>
        /// <param name="parameterName">Full SSM parameter path, e.g. /qldsv-htc/db/connection-string</param>
        /// <returns>Decrypted parameter value.</returns>
        public static string GetParameter(string parameterName)
        {
            var region = RegionEndpoint.GetBySystemName(AwsRegion);
            using (var client = new AmazonSimpleSystemsManagementClient(region))
            {
                var request = new GetParameterRequest
                {
                    Name = parameterName,
                    WithDecryption = true
                };

                // Synchronous call — acceptable in a WinForms DAL context.
                var response = client.GetParameterAsync(request)
                                     .GetAwaiter()
                                     .GetResult();

                return response.Parameter.Value;
            }
        }
    }
}
