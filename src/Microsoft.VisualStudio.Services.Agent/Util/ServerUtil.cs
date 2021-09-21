// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;

namespace Microsoft.VisualStudio.Services.Agent.Util
{
    public static class ServerUtil
    {
        private static DeploymentFlags _deploymentType;

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Maintainability", "CA2000:Dispose objects before losing scope", MessageId = "locationServer")]
        private static async Task<Location.ConnectionData> GetConnectionData(string serverUrl, VssCredentials credentials, ILocationServer locationServer)
        {
            VssConnection connection = VssUtil.CreateConnection(new Uri(serverUrl), credentials);
            await locationServer.ConnectAsync(connection);
            return await locationServer.GetConnectionDataAsync();
        }

        public static async Task<bool> IsHosted(string serverUrl, VssCredentials credentials, ILocationServer locationServer)
        {
            if (_deploymentType == DeploymentFlags.None)
            {
                var connectionData = await GetConnectionData(serverUrl, credentials, locationServer);
                _deploymentType = connectionData.DeploymentType;
            }

            switch (_deploymentType)
            {
                case DeploymentFlags.Hosted:
                    return true;
                case DeploymentFlags.OnPremises:
                    return false;
                case DeploymentFlags.None:
                    throw new Exception($"Deployment type not found");
                default:
                    throw new Exception($"Unable to recognize deployment type: '{_deploymentType}'");
            }
        }
    }
}
