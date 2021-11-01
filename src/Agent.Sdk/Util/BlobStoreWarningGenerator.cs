// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Agent.Sdk;

namespace Microsoft.VisualStudio.Services.Agent.Util
{
    public static class BlobStoreWarningGenerator
    {
        /// <summary>
        /// Used to generate warning message with platform specific allow list of domains
        /// </summary>
        /// <param name="baseMessage">Base part of the message</param>
        /// <param name="blobStoreHost">Blbe store host for which warning should be generated</param>
        /// <returns></returns>
        public static string GetPlatformSpecificWarningMessage(string baseMessage, string blobStoreHost)
        {
            var hostOS = PlatformUtil.HostOS;
            var infoURL = PlatformSpecificAllowList.WindowsAllowList;
            switch (hostOS)
            {
                case PlatformUtil.OS.Linux:
                    infoURL = PlatformSpecificAllowList.LinuxAllowList;
                    break;
                case PlatformUtil.OS.OSX:
                    infoURL = PlatformSpecificAllowList.MacOSAllowList;
                    break;
                default:
                    break;
            }

            return StringUtil.Loc(baseMessage, blobStoreHost, infoURL);
        }

        internal static class PlatformSpecificAllowList
        {
            public const string WindowsAllowList = "https://aka.ms/windows-agent-allowlist";
            public const string MacOSAllowList = "https://aka.ms/macOS-agent-allowlist";
            public const string LinuxAllowList = "https://aka.ms/linux-agent-allowlist";
        }
    }
}
