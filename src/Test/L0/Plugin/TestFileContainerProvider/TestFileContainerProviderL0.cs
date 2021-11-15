// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Agent.Plugins;
using Agent.Sdk;
using Microsoft.VisualStudio.Services.FileContainer;
using Minimatch;
using Xunit;

namespace Microsoft.VisualStudio.Services.Agent.Tests
{
    public class TestFileContainerProviderL0
    {
        [Theory]
        [Trait("Level", "L0")]
        [Trait("Category", "Plugin")]
        [InlineData(new string[] { "**" }, 7, 
            new string[] { "ArtifactForTest", "ArtifactForTest/File1.txt", "ArtifactForTest/Folder1", "ArtifactForTest/Folder1/File2.txt", 
                "ArtifactForTest/Folder1/File21.txt", "ArtifactForTest/Folder1/Folder2", "ArtifactForTest/Folder1/Folder2/File3.txt" })]
        [InlineData(new string[] { "**", "!**/File2.txt" }, 6,
            new string[] { "ArtifactForTest", "ArtifactForTest/File1.txt", "ArtifactForTest/Folder1", "ArtifactForTest/Folder1/File21.txt", 
                "ArtifactForTest/Folder1/Folder2", "ArtifactForTest/Folder1/Folder2/File3.txt" })]
        [InlineData(new string[] { "**", "!**/File2*" }, 5,
            new string[] { "ArtifactForTest", "ArtifactForTest/File1.txt", "ArtifactForTest/Folder1", "ArtifactForTest/Folder1/Folder2", 
                "ArtifactForTest/Folder1/Folder2/File3.txt" })]
        [InlineData(new string[] { "**", "!**/Folder2/**" }, 6,
            new string[] { "ArtifactForTest", "ArtifactForTest/File1.txt", "ArtifactForTest/Folder1", "ArtifactForTest/Folder1/File2.txt", 
                "ArtifactForTest/Folder1/File21.txt", "ArtifactForTest/Folder1/Folder2" })]
        [InlineData(new string[] { "**/Folder1/**", "!**/File3.txt" }, 3,
            new string[] { "ArtifactForTest/Folder1/File2.txt", "ArtifactForTest/Folder1/File21.txt", "ArtifactForTest/Folder1/Folder2" })]
        [InlineData(new string[] { "**/File*.txt", "!**/File3.txt" }, 3,
            new string[] { "ArtifactForTest/File1.txt", "ArtifactForTest/Folder1/File2.txt", "ArtifactForTest/Folder1/File21.txt" })]
        [InlineData(new string[] { "**", "!**/Folder1/**", "!!**/File3.txt" }, 4,
            new string[] { "ArtifactForTest", "ArtifactForTest/File1.txt", "ArtifactForTest/Folder1", "ArtifactForTest/Folder1/Folder2/File3.txt" })]
        [InlineData(new string[] { "**", "   !**/Folder1/**  ", "!!**/File3.txt" }, 4,
            new string[] { "ArtifactForTest", "ArtifactForTest/File1.txt", "ArtifactForTest/Folder1", "ArtifactForTest/Folder1/Folder2/File3.txt" })]
        [InlineData(new string[] { "**", "!**/Folder1/**", "#!**/Folder2/**", "!!**/File3.txt" }, 4,
            new string[] { "ArtifactForTest", "ArtifactForTest/File1.txt", "ArtifactForTest/Folder1", "ArtifactForTest/Folder1/Folder2/File3.txt" })]
        [InlineData(new string[] { "**", "!**/Folder1/**", " ", "!!**/File3.txt" }, 4,
            new string[] { "ArtifactForTest", "ArtifactForTest/File1.txt", "ArtifactForTest/Folder1", "ArtifactForTest/Folder1/Folder2/File3.txt" })]
        public async Task TestGettingArtifactItemsWithMinimatchPattern(string[] pttrn, int count, string[] paths)
        {
            using (var hostContext = new TestHostContext(this))
            {
                var context = new AgentTaskPluginExecutionContext(hostContext.GetTrace());
                var provider = new FileContainerProvider(null, context.CreateArtifactsTracer());

                List<FileContainerItem> items = new List<FileContainerItem>
                {
                    new FileContainerItem() { ItemType = ContainerItemType.Folder, Path = "ArtifactForTest" },
                    new FileContainerItem() { ItemType = ContainerItemType.File, Path = "ArtifactForTest/File1.txt" },
                    new FileContainerItem() { ItemType = ContainerItemType.Folder, Path = "ArtifactForTest/Folder1" },
                    new FileContainerItem() { ItemType = ContainerItemType.File, Path = "ArtifactForTest/Folder1/File2.txt" },
                    new FileContainerItem() { ItemType = ContainerItemType.File, Path = "ArtifactForTest/Folder1/File21.txt" },
                    new FileContainerItem() { ItemType = ContainerItemType.Folder, Path = "ArtifactForTest/Folder1/Folder2" },
                    new FileContainerItem() { ItemType = ContainerItemType.File, Path = "ArtifactForTest/Folder1/Folder2/File3.txt" }
                };
                
                string[] minimatchPatterns = pttrn;

                Options customMinimatchOptions = new Options()
                {
                    Dot = true,
                    NoBrace = true,
                    AllowWindowsPaths = PlatformUtil.RunningOnWindows
                };

                List<FileContainerItem> resultItems = provider.GetFilteredItems(items, minimatchPatterns, customMinimatchOptions);

                Assert.Equal(count, resultItems.Count);

                string listPaths = string.Join(", ", paths);
                List<string> pathsList = new List<string>();
                foreach (FileContainerItem item in resultItems)
                {
                    pathsList.Add(item.Path);
                }
                string resultPaths = string.Join(", ", pathsList);
                
                Assert.Equal(listPaths, resultPaths);
            }
        }
    }
}
