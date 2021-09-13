// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.VisualStudio.Services.Agent.Util;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using System;
using System.IO;
using System.Security.Principal;
using System.Threading.Tasks;

namespace Microsoft.VisualStudio.Services.Agent.Listener.Configuration
{
    public static class Validators
    {
        private static String UriHttpScheme = "http";
        private static String UriHttpsScheme = "https";

        public static bool ServerUrlValidator(string value)
        {
            try
            {
                Uri uri;
                if (Uri.TryCreate(value, UriKind.Absolute, out uri))
                {
                    if (uri.Scheme.Equals(UriHttpScheme, StringComparison.OrdinalIgnoreCase)
                        || uri.Scheme.Equals(UriHttpsScheme, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
            catch (Exception)
            {
                return false;
            }

            return false;
        }

        public static bool AuthSchemeValidator(string value)
        {
            return CredentialManager.CredentialTypes.ContainsKey(value);
        }

        public static bool FilePathValidator(string value)
        {
            var directoryInfo = new DirectoryInfo(value);

            if (!directoryInfo.Exists)
            {
                try
                {
                    Directory.CreateDirectory(value);
                }
                catch (Exception)
                {
                    return false;
                }
            }

            return true;
        }

        public static bool BoolValidator(string value)
        {
            return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "false", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, StringUtil.Loc("Y"), StringComparison.CurrentCultureIgnoreCase) ||
                   string.Equals(value, StringUtil.Loc("N"), StringComparison.CurrentCultureIgnoreCase);
        }

        public static bool NonEmptyValidator(string value)
        {
            return !string.IsNullOrEmpty(value);
        }

        public static bool NTAccountValidator(string arg)
        {
            if (string.IsNullOrEmpty(arg) || String.IsNullOrEmpty(arg.TrimStart('.', '\\')))
            {
                return false;
            }

            try
            {
                var logonAccount = arg.TrimStart('.');
                NTAccount ntaccount = new NTAccount(logonAccount);
                SecurityIdentifier sid = (SecurityIdentifier)ntaccount.Translate(typeof(SecurityIdentifier));
            }
            catch (IdentityNotMappedException)
            {
                return false;
            }

            return true;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Maintainability", "CA2000:Dispose objects before losing scope", MessageId = "locationServer")]
        public static async Task<bool> IsHostedServer(string serverUrl, VssCredentials credentials, ILocationServer locationServer)
        {
            // Determine the service deployment type based on connection data. (Hosted/OnPremises)
            VssConnection connection = VssUtil.CreateConnection(new Uri(serverUrl), credentials);
            await locationServer.ConnectAsync(connection);
            try
            {
                var connectionData = await locationServer.GetConnectionDataAsync();
                return connectionData.DeploymentType.HasFlag(DeploymentFlags.Hosted);
            }
            catch (Exception)
            {
                // Since the DeploymentType is Enum, deserialization exception means there is a new Enum member been added.
                // It's more likely to be Hosted since OnPremises is always behind and customer can update their agent if are on-prem
                return true;
            }
        }
    }
}
