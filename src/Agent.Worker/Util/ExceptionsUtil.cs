using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using Microsoft.VisualStudio.Services.Agent.Worker;
using Microsoft.VisualStudio.Services.Agent.Util;

namespace Agent.Worker.Util
{
    public class ExceptionsUtil
    {
        public static void HandleSocketException(SocketException e, string url, IExecutionContext trace)
        {
            trace.Error("SocketException occurred.");
            trace.Error(e.Message);
            trace.Error($"Verify whether you have (network) access to { url }");
            trace.Error($"URLs the agent need communicate with - { BlobStoreWarningInfoProvider.GetAllowListLinkForCurrentPlatform() }");
        }

        public static void HandleSocketExceptionAsync(SocketException e, string url, IAsyncCommandContext trace)
        {
            trace.Warn("SocketException occurred.");
            trace.Warn(e.Message);
            trace.Warn($"Verify whether you have (network) access to { url }");
            trace.Warn($"URLs the agent need communicate with - { BlobStoreWarningInfoProvider.GetAllowListLinkForCurrentPlatform() }");
        }
    }
}
