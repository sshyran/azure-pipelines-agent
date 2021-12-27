// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.VisualStudio.Services.Agent.Util
{
    public static class TeeUtil
    {
        private static readonly string TeeTempDir = "tee_temp_dir";

        private static readonly string TeeUrl = "https://vstsagenttools.blob.core.windows.net/tools/tee/14_135_0/TEE-CLC-14.135.0.zip";

        public static void DownloadTeeIfAbsent(
            string agentHomeDirectory,
            string agentTempDirectory,
            int downloadRetryCount,
            Action<string> debug,
            CancellationToken cancellationToken
        ) {
            if (Directory.Exists(GetTeePath(agentHomeDirectory)))
            {
                return;
            }

            // int providedDownloadRetryCount = AgentKnobs.TeePluginDownloadRetryCount.GetValue(context).AsInt();
            // int downloadRetryCount = Math.Max(providedDownloadRetryCount, 3);

            for (int downloadAttempt = 1; downloadAttempt <= downloadRetryCount; downloadAttempt++)
            {
                try
                {
                    debug($"Trying to download and extract TEE. Attempt: {downloadAttempt}");
                    DownloadAndExtractTee(agentHomeDirectory, agentTempDirectory, debug, cancellationToken);
                    break;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    debug("TEE download has been cancelled.");
                    break;
                }
                catch (Exception ex) when (downloadAttempt != downloadRetryCount)
                {
                    debug($"Failed to download TEE. Error: {ex.ToString()}");
                }
            }
        }

        private static void DownloadAndExtractTee(
            string agentHomeDirectory,
            string agentTempDirectory,
            Action<string> debug,
            CancellationToken cancellationToken
        ) {
            string tempDirectory = Path.Combine(agentTempDirectory, TeeTempDir);
            IOUtil.DeleteDirectory(tempDirectory, CancellationToken.None);
            Directory.CreateDirectory(tempDirectory);

            string zipPath = Path.Combine(tempDirectory, $"{Guid.NewGuid().ToString()}.zip");
            DownloadTee(zipPath, debug, cancellationToken).GetAwaiter().GetResult();

            debug($"Downloaded {zipPath}");

            string extractedTeePath = Path.Combine(tempDirectory, $"{Guid.NewGuid().ToString()}");
            ZipFile.ExtractToDirectory(zipPath, extractedTeePath);

            debug($"Extracted {zipPath} to ${extractedTeePath}");

            string extractedTeeDestinationPath = GetTeePath(agentHomeDirectory);
            Directory.Move(Path.Combine(extractedTeePath, "TEE-CLC-14.135.0"), extractedTeeDestinationPath);

            debug($"Moved to ${extractedTeeDestinationPath}");

            IOUtil.DeleteDirectory(tempDirectory, CancellationToken.None);

            // We have to set these files as executable because ZipFile.ExtractToDirectory does not set file permissions
            SetPermissions(Path.Combine(extractedTeeDestinationPath, "tf"), "a+x");
            SetPermissions(Path.Combine(extractedTeeDestinationPath, "native"), "a+x", recursive: true);
        }

        private static async Task DownloadTee(string zipPath, Action<string> debug, CancellationToken cancellationToken)
        {
            using (var client = new WebClient())
            using (var registration = cancellationToken.Register(client.CancelAsync))
            {
                client.DownloadProgressChanged +=
                    (_, progressEvent) => debug($"TEE download progress: {progressEvent.ProgressPercentage}%.");
                await client.DownloadFileTaskAsync(new Uri(TeeUrl), zipPath);
            }
        }

        private static void SetPermissions(string path, string permissions, bool recursive = false)
        {
            var chmodProcessInfo = new ProcessStartInfo("chmod")
            {
                Arguments = $"{permissions} {(recursive ? "-R" : "")} {path}",
                UseShellExecute = false,
                RedirectStandardError = true
            };
            Process chmodProcess = Process.Start(chmodProcessInfo);
            chmodProcess.WaitForExit();

            string chmodStderr = chmodProcess.StandardError.ReadToEnd();
            if (chmodStderr.Length != 0 || chmodProcess.ExitCode != 0)
            {
                throw new Exception($"Failed to set {path} permissions to {permissions} (recursive: {recursive}). Exit code: {chmodProcess.ExitCode}; stderr: {chmodStderr}");
            }
        }

        public static void DeleteTee(string agentHomeDirectory, string agentTempDirectory, Action<string> debug)
        {
            string teeDirectory = GetTeePath(agentHomeDirectory);
            IOUtil.DeleteDirectory(teeDirectory, CancellationToken.None);

            string tempDirectory = Path.Combine(agentTempDirectory, TeeTempDir);
            IOUtil.DeleteDirectory(tempDirectory, CancellationToken.None);

            debug($"Cleaned up {teeDirectory} and {tempDirectory}");
        }

        private static string GetTeePath(string agentHomeDirectory)
        {
            return Path.Combine(agentHomeDirectory, "externals", "tee");
        }
    }
}
