// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Agent.Sdk;

namespace Microsoft.VisualStudio.Services.Agent.Util
{
    public static class BlobStoreWarningGenerator
    {
        /// <summary>
        /// Used to get platform-specific reference to allow list in agent documenation page
        /// </summary>
        public static string GetPlatformAllowListLink()
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

            return infoURL;
        }

        internal static class PlatformSpecificAllowList
        {
            public const string WindowsAllowList = "https://aka.ms/windows-agent-allowlist";
            public const string MacOSAllowList = "https://aka.ms/macOS-agent-allowlist";
            public const string LinuxAllowList = "https://aka.ms/linux-agent-allowlist";
        }
    }
}
