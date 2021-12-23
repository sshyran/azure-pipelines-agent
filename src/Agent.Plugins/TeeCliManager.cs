// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Agent.Sdk;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Microsoft.VisualStudio.Services.Agent.Util;
using Agent.Sdk.Knob;

namespace Agent.Plugins.Repository
{
    public sealed class TeeCliManager : TfsVCCliManager, ITfsVCCliManager
    {
        public override TfsVCFeatures Features => TfsVCFeatures.Eula;

        protected override string Switch => "-";

        public static readonly int RetriesOnFailure = 3;

        public string FilePath => Path.Combine(ExecutionContext.Variables.GetValueOrDefault("agent.homedirectory")?.Value, "externals", "tee", "tf");

        private static readonly string TeeTempDir = "tee_temp_dir";

        private static readonly string TeeUrl = "https://vstsagenttools.blob.core.windows.net/tools/tee/14_135_0/TEE-CLC-14.135.0.zip";

        // TODO: Remove AddAsync after last-saved-checkin-metadata problem is fixed properly.
        public async Task AddAsync(string localPath)
        {
            ArgUtil.NotNullOrEmpty(localPath, nameof(localPath));
            await RunPorcelainCommandAsync(FormatFlags.OmitCollectionUrl, "add", localPath);
        }

        public void CleanupProxySetting()
        {
            // no-op for TEE.
        }

        public async Task EulaAsync()
        {
            await RunCommandAsync(FormatFlags.All, "eula", "-accept");
        }

        public async Task GetAsync(string localPath, bool quiet = false)
        {
            ArgUtil.NotNullOrEmpty(localPath, nameof(localPath));
            await RunCommandAsync(FormatFlags.OmitCollectionUrl, quiet, 3, "get", $"-version:{SourceVersion}", "-recursive", "-overwrite", localPath);
        }

        public string ResolvePath(string serverPath)
        {
            ArgUtil.NotNullOrEmpty(serverPath, nameof(serverPath));
            string localPath = RunPorcelainCommandAsync("resolvePath", $"-workspace:{WorkspaceName}", serverPath).GetAwaiter().GetResult();
            localPath = localPath?.Trim();

            // Paths outside of the root mapping return empty.
            // Paths within a cloaked directory return "null".
            if (string.IsNullOrEmpty(localPath) ||
                string.Equals(localPath, "null", StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            return localPath;
        }

        public Task ScorchAsync()
        {
            throw new NotSupportedException();
        }

        public void SetupProxy(string proxyUrl, string proxyUsername, string proxyPassword)
        {
            if (!string.IsNullOrEmpty(proxyUrl))
            {
                Uri proxy = UrlUtil.GetCredentialEmbeddedUrl(new Uri(proxyUrl), proxyUsername, proxyPassword);
                AdditionalEnvironmentVariables["http_proxy"] = proxy.AbsoluteUri;
            }
        }

        public void SetupClientCertificate(string clientCert, string clientCertKey, string clientCertArchive, string clientCertPassword)
        {
            ExecutionContext.Debug("Convert client certificate from 'pkcs' format to 'jks' format.");
            string toolPath = WhichUtil.Which("keytool", true, ExecutionContext);
            string jksFile = Path.Combine(ExecutionContext.Variables.GetValueOrDefault("agent.tempdirectory")?.Value, $"{Guid.NewGuid()}.jks");
            string argLine;
            if (!string.IsNullOrEmpty(clientCertPassword))
            {
                argLine = $"-importkeystore -srckeystore \"{clientCertArchive}\" -srcstoretype pkcs12 -destkeystore \"{jksFile}\" -deststoretype JKS -srcstorepass \"{clientCertPassword}\" -deststorepass \"{clientCertPassword}\"";
            }
            else
            {
                argLine = $"-importkeystore -srckeystore \"{clientCertArchive}\" -srcstoretype pkcs12 -destkeystore \"{jksFile}\" -deststoretype JKS";
            }

            ExecutionContext.Command($"{toolPath} {argLine}");

            using (var processInvoker = new ProcessInvoker(ExecutionContext))
            {
                processInvoker.OutputDataReceived += (object sender, ProcessDataReceivedEventArgs args) =>
                {
                    if (!string.IsNullOrEmpty(args.Data))
                    {
                        ExecutionContext.Output(args.Data);
                    }
                };
                processInvoker.ErrorDataReceived += (object sender, ProcessDataReceivedEventArgs args) =>
                {
                    if (!string.IsNullOrEmpty(args.Data))
                    {
                        ExecutionContext.Output(args.Data);
                    }
                };

                processInvoker.ExecuteAsync(ExecutionContext.Variables.GetValueOrDefault("system.defaultworkingdirectory")?.Value, toolPath, argLine, null, true, CancellationToken.None).GetAwaiter().GetResult();

                if (!string.IsNullOrEmpty(clientCertPassword))
                {
                    ExecutionContext.Debug($"Set TF_ADDITIONAL_JAVA_ARGS=-Djavax.net.ssl.keyStore={jksFile} -Djavax.net.ssl.keyStorePassword={clientCertPassword}");
                    AdditionalEnvironmentVariables["TF_ADDITIONAL_JAVA_ARGS"] = $"-Djavax.net.ssl.keyStore={jksFile} -Djavax.net.ssl.keyStorePassword={clientCertPassword}";
                }
                else
                {
                    ExecutionContext.Debug($"Set TF_ADDITIONAL_JAVA_ARGS=-Djavax.net.ssl.keyStore={jksFile}");
                    AdditionalEnvironmentVariables["TF_ADDITIONAL_JAVA_ARGS"] = $"-Djavax.net.ssl.keyStore={jksFile}";
                }
            }
        }

        public async Task ShelveAsync(string shelveset, string commentFile, bool move)
        {
            ArgUtil.NotNullOrEmpty(shelveset, nameof(shelveset));
            ArgUtil.NotNullOrEmpty(commentFile, nameof(commentFile));

            // TODO: Remove parameter move after last-saved-checkin-metadata problem is fixed properly.
            if (move)
            {
                await RunPorcelainCommandAsync(FormatFlags.OmitCollectionUrl, "shelve", $"-workspace:{WorkspaceName}", "-move", "-replace", "-recursive", $"-comment:@{commentFile}", shelveset);
                return;
            }

            await RunPorcelainCommandAsync(FormatFlags.OmitCollectionUrl, "shelve", $"-workspace:{WorkspaceName}", "-saved", "-replace", "-recursive", $"-comment:@{commentFile}", shelveset);
        }

        public async Task<ITfsVCShelveset> ShelvesetsAsync(string shelveset)
        {
            ArgUtil.NotNullOrEmpty(shelveset, nameof(shelveset));
            string output = await RunPorcelainCommandAsync("shelvesets", "-format:xml", $"-workspace:{WorkspaceName}", shelveset);
            string xml = ExtractXml(output);

            // Deserialize the XML.
            // The command returns a non-zero exit code if the shelveset is not found.
            // The assertions performed here should never fail.
            var serializer = new XmlSerializer(typeof(TeeShelvesets));
            ArgUtil.NotNullOrEmpty(xml, nameof(xml));
            using (var reader = new StringReader(xml))
            {
                var teeShelvesets = serializer.Deserialize(reader) as TeeShelvesets;
                ArgUtil.NotNull(teeShelvesets, nameof(teeShelvesets));
                ArgUtil.NotNull(teeShelvesets.Shelvesets, nameof(teeShelvesets.Shelvesets));
                ArgUtil.Equal(1, teeShelvesets.Shelvesets.Length, nameof(teeShelvesets.Shelvesets.Length));
                return teeShelvesets.Shelvesets[0];
            }
        }

        public async Task<ITfsVCStatus> StatusAsync(string localPath)
        {
            ArgUtil.NotNullOrEmpty(localPath, nameof(localPath));
            string output = await RunPorcelainCommandAsync(FormatFlags.OmitCollectionUrl, "status", "-recursive", "-nodetect", "-format:xml", localPath);
            string xml = ExtractXml(output);
            var serializer = new XmlSerializer(typeof(TeeStatus));
            using (var reader = new StringReader(xml ?? string.Empty))
            {
                return serializer.Deserialize(reader) as TeeStatus;
            }
        }

        public bool TestEulaAccepted()
        {
            // Resolve the path to the XML file containing the EULA-accepted flag.
            string homeDirectory = Environment.GetEnvironmentVariable("HOME");
            if (!string.IsNullOrEmpty(homeDirectory) && Directory.Exists(homeDirectory))
            {
                string tfDataDirectory = (PlatformUtil.RunningOnMacOS)
                    ? Path.Combine("Library", "Application Support", "Microsoft")
                    : ".microsoft";

                string xmlFile = Path.Combine(
                    homeDirectory,
                    tfDataDirectory,
                    "Team Foundation",
                    "4.0",
                    "Configuration",
                    "TEE-Mementos",
                    "com.microsoft.tfs.client.productid.xml");

                if (File.Exists(xmlFile))
                {
                    // Load and deserialize the XML.
                    string xml = File.ReadAllText(xmlFile, Encoding.UTF8);
                    XmlSerializer serializer = new XmlSerializer(typeof(ProductIdData));
                    using (var reader = new StringReader(xml ?? string.Empty))
                    {
                        var data = serializer.Deserialize(reader) as ProductIdData;
                        return string.Equals(data?.Eula?.Value ?? string.Empty, "true", StringComparison.OrdinalIgnoreCase);
                    }
                }
            }

            return false;
        }

        public override async Task<bool> TryWorkspaceDeleteAsync(ITfsVCWorkspace workspace)
        {
            ArgUtil.NotNull(workspace, nameof(workspace));
            try
            {
                await RunCommandAsync("workspace", "-delete", $"{workspace.Name};{workspace.Owner}");
                return true;
            }
            catch (Exception ex)
            {
                ExecutionContext.Warning(ex.Message);
                return false;
            }
        }

        public async Task UndoAsync(string localPath)
        {
            ArgUtil.NotNullOrEmpty(localPath, nameof(localPath));
            await RunCommandAsync(FormatFlags.OmitCollectionUrl, "undo", "-recursive", localPath);
        }

        public async Task UnshelveAsync(string shelveset, bool failOnNonZeroExitCode = true)
        {
            ArgUtil.NotNullOrEmpty(shelveset, nameof(shelveset));
            await RunCommandAsync(FormatFlags.OmitCollectionUrl, false, failOnNonZeroExitCode, "unshelve", "-format:detailed", $"-workspace:{WorkspaceName}", shelveset);
        }

        public async Task WorkfoldCloakAsync(string serverPath)
        {
            ArgUtil.NotNullOrEmpty(serverPath, nameof(serverPath));
            await RunCommandAsync(3, "workfold", "-cloak", $"-workspace:{WorkspaceName}", serverPath);
        }

        public async Task WorkfoldMapAsync(string serverPath, string localPath)
        {
            ArgUtil.NotNullOrEmpty(serverPath, nameof(serverPath));
            ArgUtil.NotNullOrEmpty(localPath, nameof(localPath));
            await RunCommandAsync(3, "workfold", "-map", $"-workspace:{WorkspaceName}", serverPath, localPath);
        }

        public Task WorkfoldUnmapAsync(string serverPath)
        {
            throw new NotSupportedException();
        }

        public async Task WorkspaceDeleteAsync(ITfsVCWorkspace workspace)
        {
            ArgUtil.NotNull(workspace, nameof(workspace));
            await RunCommandAsync("workspace", "-delete", $"{workspace.Name};{workspace.Owner}");
        }

        public async Task WorkspaceNewAsync()
        {
            await RunCommandAsync("workspace", "-new", "-location:local", "-permission:Public", WorkspaceName);
        }

        public async Task<ITfsVCWorkspace[]> WorkspacesAsync(bool matchWorkspaceNameOnAnyComputer = false)
        {
            // Build the args.
            var args = new List<string>();
            args.Add("workspaces");
            if (matchWorkspaceNameOnAnyComputer)
            {
                args.Add(WorkspaceName);
                args.Add($"-computer:*");
            }

            args.Add("-format:xml");

            // Run the command.
            TfsVCPorcelainCommandResult result = await TryRunPorcelainCommandAsync(FormatFlags.None, RetriesOnFailure, args.ToArray());
            ArgUtil.NotNull(result, nameof(result));
            if (result.Exception != null)
            {
                // Check if the workspace name was specified and the command returned exit code 1.
                if (matchWorkspaceNameOnAnyComputer && result.Exception.ExitCode == 1)
                {
                    // Ignore the error. This condition can indicate the workspace was not found.
                    return new ITfsVCWorkspace[0];
                }

                // Dump the output and throw.
                result.Output?.ForEach(x => ExecutionContext.Output(x ?? string.Empty));
                throw result.Exception;
            }

            // Note, string.join gracefully handles a null element within the IEnumerable<string>.
            string output = string.Join(Environment.NewLine, result.Output ?? new List<string>()) ?? string.Empty;
            string xml = ExtractXml(output);

            // Deserialize the XML.
            var serializer = new XmlSerializer(typeof(TeeWorkspaces));
            using (var reader = new StringReader(xml))
            {
                return (serializer.Deserialize(reader) as TeeWorkspaces)
                    ?.Workspaces
                    ?.Cast<ITfsVCWorkspace>()
                    .ToArray();
            }
        }

        public override async Task WorkspacesRemoveAsync(ITfsVCWorkspace workspace)
        {
            ArgUtil.NotNull(workspace, nameof(workspace));
            await RunCommandAsync("workspace", $"-remove:{workspace.Name};{workspace.Owner}");
        }

        private static string ExtractXml(string output)
        {
            // tf commands that output XML, may contain information messages preceeding the XML content.
            //
            // For example, the workspaces subcommand returns a non-XML message preceeding the XML when there are no workspaces.
            //
            // Also for example, when JAVA_TOOL_OPTIONS is set, a message like "Picked up JAVA_TOOL_OPTIONS: -Dfile.encoding=UTF8"
            // may preceed the XML content.
            output = output ?? string.Empty;
            int xmlIndex = output.IndexOf("<?xml");
            if (xmlIndex > 0)
            {
                return output.Substring(xmlIndex);
            }

            return output;
        }

        // Download TEE if absent
        public void DownloadResources()
        {
            try
            {
                DownloadAndExtractTeeWithRetries();
            }
            catch (Exception ex)
            {
                ExecutionContext.Warning($"Failed to download Resources. Trying to clean up.");
                CleanupFiles();
                throw;
            }
        }

        private void DownloadAndExtractTeeWithRetries()
        {
            if (Directory.Exists(Path.GetDirectoryName(FilePath)))
            {
                return;
            }

            int providedDownloadRetryCount = AgentKnobs.TeePluginDownloadRetryCount.GetValue(ExecutionContext).AsInt();
            int downloadRetryCount = Math.Max(providedDownloadRetryCount, 3);

            for (int downloadAttempt = 1; downloadAttempt <= downloadRetryCount; downloadAttempt++)
            {
                try
                {
                    ExecutionContext.Debug($"Trying to download and extract TEE. Attempt: {downloadAttempt}");
                    DownloadAndExtractTee();
                    break;
                }
                catch (OperationCanceledException) when (CancellationToken.IsCancellationRequested)
                {
                    ExecutionContext.Info("TEE download has been cancelled.");
                    break;
                }
                catch (Exception ex) when (downloadAttempt != downloadRetryCount)
                {
                    ExecutionContext.Warning($"Failed to download TEE. Error: {ex.ToString()}");
                }
            }
        }

        private void DownloadAndExtractTee()
        {
            string tempDirectory = Path.Combine(ExecutionContext.Variables.GetValueOrDefault("Agent.TempDirectory")?.Value, TeeTempDir);
            IOUtil.DeleteDirectory(tempDirectory, CancellationToken.None);
            Directory.CreateDirectory(tempDirectory);

            string zipPath = Path.Combine(tempDirectory, $"{Guid.NewGuid().ToString()}.zip");
            DownloadTee(zipPath).GetAwaiter().GetResult();

            ExecutionContext.Debug($"Downloaded {zipPath}");

            string extractedTeePath = Path.Combine(tempDirectory, $"{Guid.NewGuid().ToString()}");
            ZipFile.ExtractToDirectory(zipPath, extractedTeePath);

            ExecutionContext.Debug($"Extracted {zipPath} to ${extractedTeePath}");

            // We have to set these files as executable because ZipFile.ExtractToDirectory does not set file permissions
            SetPermissions(Path.Combine(extractedTeePath, "tf"), "a+x");
            SetPermissions(Path.Combine(extractedTeePath, "native"), "a+x", recursive: true);

            string extractedTeeDestinationPath = Path.GetDirectoryName(FilePath);
            Directory.Move(Path.Combine(extractedTeePath, "TEE-CLC-14.135.0"), extractedTeeDestinationPath);

            ExecutionContext.Debug($"Moved to ${extractedTeeDestinationPath}");

            IOUtil.DeleteDirectory(tempDirectory, CancellationToken.None);
        }

        private async Task DownloadTee(string zipPath)
        {
            using (var client = new WebClient())
            using (var registration = CancellationToken.Register(client.CancelAsync))
            {
                client.DownloadProgressChanged +=
                    (_, progressEvent) => ExecutionContext.Debug($"TEE download progress: {progressEvent.ProgressPercentage}%.");
                await client.DownloadFileTaskAsync(new Uri(TeeUrl), zipPath);
            }
        }

        private void SetPermissions(string filePath, string permissions, bool recursive = false)
        {
            var chmodProcessInfo = new ProcessStartInfo("chmod")
            {
                Arguments = $"{permissions} {(recursive ? "-R" : "")}",
                UseShellExecute = false,
                RedirectStandardError = true
            };
            Process chmodProcess = Process.Start(chmodProcessInfo);
            chmodProcess.WaitForExit();

            string chmodStderr = chmodProcess.StandardError.ReadToEnd();
            if (chmodStderr.Length != 0 || chmodProcess.ExitCode != 0)
            {
                throw new Exception($"Failed to set {filePath} permissions to {permissions} (recursive: {recursive}). Exit code: {chmodProcess.ExitCode}; stderr: {chmodStderr}");
            }
        }

        public void DeleteResources()
        {
            if (AgentKnobs.DisableTeePluginRemoval.GetValue(ExecutionContext).AsBoolean())
            {
                return;
            }

            CleanupFiles();
        }

        private void CleanupFiles()
        {
            IOUtil.DeleteDirectory(Path.GetDirectoryName(FilePath), CancellationToken.None);

            string tempDirectory = Path.Combine(ExecutionContext.Variables.GetValueOrDefault("Agent.TempDirectory")?.Value, TeeTempDir);
            IOUtil.DeleteDirectory(tempDirectory, CancellationToken.None);
        }

        ////////////////////////////////////////////////////////////////////////////////
        // Product ID data objects (required for testing whether the EULA has been accepted).
        ////////////////////////////////////////////////////////////////////////////////
        [XmlRoot(ElementName = "ProductIdData", Namespace = "")]
        public sealed class ProductIdData
        {
            [XmlElement(ElementName = "eula-14.0", Namespace = "")]
            public Eula Eula { get; set; }
        }

        public sealed class Eula
        {
            [XmlAttribute(AttributeName = "value", Namespace = "")]
            public string Value { get; set; }
        }
    }

    ////////////////////////////////////////////////////////////////////////////////
    // tf shelvesets data objects
    ////////////////////////////////////////////////////////////////////////////////
    [XmlRoot(ElementName = "shelvesets", Namespace = "")]
    public sealed class TeeShelvesets
    {
        [XmlElement(ElementName = "shelveset", Namespace = "")]
        public TeeShelveset[] Shelvesets { get; set; }
    }

    public sealed class TeeShelveset : ITfsVCShelveset
    {
        [XmlAttribute(AttributeName = "date", Namespace = "")]
        public string Date { get; set; }

        [XmlAttribute(AttributeName = "name", Namespace = "")]
        public string Name { get; set; }

        [XmlAttribute(AttributeName = "owner", Namespace = "")]
        public string Owner { get; set; }

        [XmlElement(ElementName = "comment", Namespace = "")]
        public string Comment { get; set; }
    }

    ////////////////////////////////////////////////////////////////////////////////
    // tf status data objects.
    ////////////////////////////////////////////////////////////////////////////////
    [XmlRoot(ElementName = "status", Namespace = "")]
    public sealed class TeeStatus : ITfsVCStatus
    {
        // Elements.
        [XmlArray(ElementName = "candidate-pending-changes", Namespace = "")]
        [XmlArrayItem(ElementName = "pending-change", Namespace = "")]
        public TeePendingChange[] CandidatePendingChanges { get; set; }

        [XmlArray(ElementName = "pending-changes", Namespace = "")]
        [XmlArrayItem(ElementName = "pending-change", Namespace = "")]
        public TeePendingChange[] PendingChanges { get; set; }

        // Interface-only properties.
        [XmlIgnore]
        public IEnumerable<ITfsVCPendingChange> AllAdds
        {
            get
            {
                return PendingChanges?.Where(x => string.Equals(x.ChangeType, "add", StringComparison.OrdinalIgnoreCase));
            }
        }

        [XmlIgnore]
        public bool HasPendingChanges => PendingChanges?.Any() ?? false;
    }

    public sealed class TeePendingChange : ITfsVCPendingChange
    {
        [XmlAttribute(AttributeName = "change-type", Namespace = "")]
        public string ChangeType { get; set; }

        [XmlAttribute(AttributeName = "computer", Namespace = "")]
        public string Computer { get; set; }

        [XmlAttribute(AttributeName = "date", Namespace = "")]
        public string Date { get; set; }

        [XmlAttribute(AttributeName = "file-type", Namespace = "")]
        public string FileType { get; set; }

        [XmlAttribute(AttributeName = "local-item", Namespace = "")]
        public string LocalItem { get; set; }

        [XmlAttribute(AttributeName = "lock", Namespace = "")]
        public string Lock { get; set; }

        [XmlAttribute(AttributeName = "owner", Namespace = "")]
        public string Owner { get; set; }

        [XmlAttribute(AttributeName = "server-item", Namespace = "")]
        public string ServerItem { get; set; }

        [XmlAttribute(AttributeName = "version", Namespace = "")]
        public string Version { get; set; }

        [XmlAttribute(AttributeName = "workspace", Namespace = "")]
        public string Workspace { get; set; }
    }

    ////////////////////////////////////////////////////////////////////////////////
    // tf workspaces data objects.
    ////////////////////////////////////////////////////////////////////////////////
    [XmlRoot(ElementName = "workspaces", Namespace = "")]
    public sealed class TeeWorkspaces
    {
        [XmlElement(ElementName = "workspace", Namespace = "")]
        public TeeWorkspace[] Workspaces { get; set; }
    }

    public sealed class TeeWorkspace : ITfsVCWorkspace
    {
        // Attributes.
        [XmlAttribute(AttributeName = "server", Namespace = "")]
        public string CollectionUrl { get; set; }

        [XmlAttribute(AttributeName = "comment", Namespace = "")]
        public string Comment { get; set; }

        [XmlAttribute(AttributeName = "computer", Namespace = "")]
        public string Computer { get; set; }

        [XmlAttribute(AttributeName = "name", Namespace = "")]
        public string Name { get; set; }

        [XmlAttribute(AttributeName = "owner", Namespace = "")]
        public string Owner { get; set; }

        // Elements.
        [XmlElement(ElementName = "working-folder", Namespace = "")]
        public TeeMapping[] TeeMappings { get; set; }

        // Interface-only properties.
        [XmlIgnore]
        public ITfsVCMapping[] Mappings => TeeMappings?.Cast<ITfsVCMapping>().ToArray();
    }

    public sealed class TeeMapping : ITfsVCMapping
    {
        [XmlIgnore]
        public bool Cloak => string.Equals(MappingType, "cloak", StringComparison.OrdinalIgnoreCase);

        [XmlAttribute(AttributeName = "depth", Namespace = "")]
        public string Depth { get; set; }

        [XmlAttribute(AttributeName = "local-item", Namespace = "")]
        public string LocalPath { get; set; }

        [XmlAttribute(AttributeName = "type", Namespace = "")]
        public string MappingType { get; set; }

        [XmlIgnore]
        public bool Recursive => string.Equals(Depth, "full", StringComparison.OrdinalIgnoreCase);

        [XmlAttribute(AttributeName = "server-item")]
        public string ServerPath { get; set; }
    }
}
