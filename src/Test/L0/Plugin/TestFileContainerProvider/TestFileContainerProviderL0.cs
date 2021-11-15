// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Agent.Plugins;
using Agent.Sdk;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.FileContainer;
using Microsoft.VisualStudio.Services.WebApi;
using Minimatch;
using Moq;
using Xunit;

namespace Microsoft.VisualStudio.Services.Agent.Tests
{
    public class TestFileContainerProviderL0
    {
        //private const string TestSourceFolder = "sourceFolder";
        private const string TestDestFolder = "destFolder";
        private const string TestDownloadSourceFolder = "sourceDownloadFolder";

        //[Fact]
        //[Trait("Level", "L0")]
        //[Trait("Category", "Plugin")]
        //public async Task TestDownloadBuildArtifactAsyncWithMinimatchPattern()
        //{
        //    // Create source files in artifact
        //    byte[] sourceContent1 = GenerateRandomData();
        //    byte[] sourceContent2 = GenerateRandomData();
        //    byte[] sourceContent3 = GenerateRandomData();
        //    byte[] sourceContent4 = GenerateRandomData();
        //    TestFile sourceFile1 = new TestFile(sourceContent1);
        //    TestFile sourceFile2 = new TestFile(sourceContent2);
        //    TestFile sourceFile3 = new TestFile(sourceContent3);
        //    TestFile sourceFile4 = new TestFile(sourceContent4);
        //
        //    sourceFile1.PlaceItem(Path.Combine(Directory.GetCurrentDirectory(), Path.Combine(TestDownloadSourceFolder, "drop/file1.txt")));
        //    sourceFile2.PlaceItem(Path.Combine(Directory.GetCurrentDirectory(), Path.Combine(TestDownloadSourceFolder, "drop/dir1/file2.txt")));
        //    sourceFile3.PlaceItem(Path.Combine(Directory.GetCurrentDirectory(), Path.Combine(TestDownloadSourceFolder, "drop/dir1/file3.txt")));
        //    sourceFile4.PlaceItem(Path.Combine(Directory.GetCurrentDirectory(), Path.Combine(TestDownloadSourceFolder, "drop/dir2/dir3/file4.txt")));
        //
        //    using(var hostContext = new TestHostContext(this))
        //    {
        //        var vssConnection = new Mock<VssConnection>(new Uri("http://fake"), new VssCredentials());
        //        var context = new AgentTaskPluginExecutionContext(hostContext.GetTrace());
        //        var provider = new FileContainerProvider(vssConnection.Object, context.CreateArtifactsTracer(), true);
        //
        //        //string sourcePath = Path.Combine(Directory.GetCurrentDirectory(), TestDownloadSourceFolder);
        //        string sourcePath = Path.Combine("#/7029767/", TestDownloadSourceFolder);
        //        string destPath = Path.Combine(Directory.GetCurrentDirectory(), TestDestFolder);
        //
        //        ArtifactDownloadParameters downloadParameters = new ArtifactDownloadParameters();
        //        downloadParameters.TargetDirectory = destPath;
        //        downloadParameters.MinimatchFilters = new string[] { "**" };
        //
        //        BuildArtifact buildArtifact = new BuildArtifact();
        //        buildArtifact.Name = "drop";
        //        buildArtifact.Resource = new ArtifactResource();
        //        buildArtifact.Resource.Data = sourcePath;
        //
        //        // Download files from artifact in accordance with patterns
        //        await provider.DownloadMultipleArtifactsAsync(downloadParameters, new List<BuildArtifact> { buildArtifact }, CancellationToken.None, context);
        //
        //        // Assert
        //        var sourceFiles = Directory.GetFiles(sourcePath);
        //        var destFiles = Directory.GetFiles(Path.Combine(destPath, buildArtifact.Name));
        //        Assert.Equal(4, destFiles.Length);
        //        foreach (var file in sourceFiles)
        //        {
        //            string destFile = destFiles.FirstOrDefault(f => Path.GetFileName(f).Equals(Path.GetFileName(file)));
        //            Assert.True(StructuralComparisons.StructuralEqualityComparer.Equals(ComputeHash(file), ComputeHash(destFile)));
        //        }
        //
        //        TestCleanup();
        //    }
        //}
        
        //[Fact]
        //[Trait("Level", "L0")]
        //[Trait("Category", "Plugin")]
        //public async Task TestGettingArtifactItemsWithMinimatchPattern()
        //{
        //    using (var hostContext = new TestHostContext(this))
        //    {
        //        //ArtifactDownloadParameters downloadParameters = new ArtifactDownloadParameters();
        //        //downloadParameters.TargetDirectory = destPath;
        //        //downloadParameters.MinimatchFilters = new string[] { "**" };
        //
        //        //BuildArtifact buildArtifact = new BuildArtifact();
        //        //buildArtifact.Name = "drop";
        //        //buildArtifact.Resource = new ArtifactResource();
        //        //buildArtifact.Resource.Data = sourcePath;
        //        List<FileContainerItem> items = new List<FileContainerItem>();
        //        string[] minimatchPatterns = { };
        //        Options customMinimatchOptions = new Options();
        //
        //        IEnumerable <FileContainerItem> resultItems = await FileContainerProvider.GetFilteredItems(items, minimatchPatterns, customMinimatchOptions);
        //    }
        //}

        private void TestCleanup()
        {
            DirectoryInfo destDir = new DirectoryInfo(TestDestFolder);
        
            foreach (FileInfo file in destDir.GetFiles("*", SearchOption.AllDirectories))
            {
                file.Delete();
            }
        
            foreach (DirectoryInfo dir in destDir.EnumerateDirectories())
            {
                dir.Delete(true);
            }
        }
        
        private byte[] GenerateRandomData()
        {
            byte[] data = new byte[1024];
            Random rng = new Random();
            rng.NextBytes(data);
            return data;
        }
        
        private byte[] ComputeHash(string filePath)
        {
            using (var md5 = MD5.Create())
            {
                return md5.ComputeHash(File.ReadAllBytes(filePath));
            }
        }
    }
}
