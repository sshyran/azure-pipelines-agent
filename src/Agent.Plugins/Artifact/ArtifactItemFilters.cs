using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Agent.Sdk;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.VisualStudio.Services.BlobStore.Common;
using Microsoft.VisualStudio.Services.Content.Common.Tracing;
using Microsoft.VisualStudio.Services.FileContainer;
using Minimatch;

using Agent.Sdk.Knob;
using Agent.Sdk.Util;
using BuildXL.Cache.ContentStore.Hashing;
using Microsoft.TeamFoundation.DistributedTask.WebApi;
using Microsoft.VisualStudio.Services.Agent.Blob;
using Microsoft.VisualStudio.Services.Agent.Util;
using Microsoft.VisualStudio.Services.BlobStore.WebApi;
using Microsoft.VisualStudio.Services.Content.Common;
using Microsoft.VisualStudio.Services.FileContainer.Client;
using Microsoft.VisualStudio.Services.WebApi;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks.Dataflow;


namespace Agent.Plugins
{
    class ArtifactItemFilters
    {
        private readonly VssConnection connection;
        private readonly IAppTraceSource tracer;

        public ArtifactItemFilters(VssConnection connection, IAppTraceSource tracer)
        {
            this.tracer = tracer;
            this.connection = connection;
        }

        // Returns all artifact items. Uses minimatch filters specified in downloadParameters.
        public async Task<IEnumerable<FileContainerItem>> GetArtifactItems(ArtifactDownloadParameters downloadParameters, BuildArtifact buildArtifact)
        {
            FileContainerProvider fileContainerProvider = new FileContainerProvider(connection, tracer);
            (long, string) containerIdAndRoot = fileContainerProvider.ParseContainerId(buildArtifact.Resource.Data);
            Guid projectId = downloadParameters.ProjectId;
            string[] minimatchPatterns = downloadParameters.MinimatchFilters;

            List<FileContainerItem> items = await fileContainerProvider.containerClient.QueryContainerItemsAsync(
                containerIdAndRoot.Item1,
                projectId,
                isShallow: false,
                includeBlobMetadata: true,
                containerIdAndRoot.Item2
            );

            Options customMinimatchOptions;
            if (downloadParameters.CustomMinimatchOptions != null)
            {
                customMinimatchOptions = downloadParameters.CustomMinimatchOptions;
            }
            else
            {
                customMinimatchOptions = new Options()
                {
                    Dot = true,
                    NoBrace = true,
                    AllowWindowsPaths = PlatformUtil.RunningOnWindows
                };
            }

            List<string> paths = new List<string>();
            foreach (FileContainerItem item in items)
            {
                paths.Add(item.Path);
            }

            List<FileContainerItem> resultItems = GetFilteredItems(paths, items.GetType(), minimatchPatterns, customMinimatchOptions);

            tracer.Info($"{resultItems.Count} final results");

            IEnumerable<FileContainerItem> excludedItems = items.Except(resultItems);
            foreach (FileContainerItem item in excludedItems)
            {
                tracer.Info($"Item excluded: {item.Path}");
            }

            return resultItems;
        }

        public dynamic GetFilteredItems(List<string> items, Type typeofitems, string[] minimatchPatterns, Options customMinimatchOptions)
        {
            // Hashtable to keep track of matches.
            Hashtable map = new Hashtable();

            foreach (string minimatchPattern in minimatchPatterns)
            {
                tracer.Info($"Pattern: {minimatchPattern}");

                // Trim and skip empty.
                string currentPattern = minimatchPattern.Trim();
                if (String.IsNullOrEmpty(currentPattern))
                {
                    tracer.Info($"Skipping empty pattern.");
                    continue;
                }

                // Clone match options.
                Options matchOptions = CloneMiniMatchOptions(customMinimatchOptions);

                // Skip comments.
                if (!matchOptions.NoComment && currentPattern.StartsWith('#'))
                {
                    tracer.Info($"Skipping comment.");
                    continue;
                }

                // Set NoComment. Brace expansion could result in a leading '#'.
                matchOptions.NoComment = true;

                // Determine whether pattern is include or exclude.
                int negateCount = 0;
                if (!matchOptions.NoNegate)
                {
                    while (negateCount < currentPattern.Length && currentPattern[negateCount] == '!')
                    {
                        negateCount++;
                    }

                    currentPattern = currentPattern.Substring(negateCount);
                    if (negateCount > 0)
                    {
                        tracer.Info($"Trimmed leading '!'. Pattern: '{currentPattern}'");
                    }
                }

                bool isIncludePattern = negateCount == 0 ||
                    (negateCount % 2 == 0 && !matchOptions.FlipNegate) ||
                    (negateCount % 2 == 1 && matchOptions.FlipNegate);

                // Set NoNegate. Brace expansion could result in a leading '!'.
                matchOptions.NoNegate = true;
                matchOptions.FlipNegate = false;

                // Trim and skip empty.
                currentPattern = currentPattern.Trim();
                if (String.IsNullOrEmpty(currentPattern))
                {
                    tracer.Info($"Skipping empty pattern.");
                    continue;
                }

                // Expand braces - required to accurately interpret findPath.
                string[] expandedPatterns;
                string preExpandedPattern = currentPattern;
                if (matchOptions.NoBrace)
                {
                    expandedPatterns = new string[] { currentPattern };
                }
                else
                {
                    expandedPatterns = ExpandBraces(currentPattern, matchOptions);
                }

                // Set NoBrace.
                matchOptions.NoBrace = true;

                foreach (string expandedPattern in expandedPatterns)
                {
                    if (expandedPattern != preExpandedPattern)
                    {
                        tracer.Info($"Pattern: {expandedPattern}");
                    }

                    // Trim and skip empty.
                    currentPattern = expandedPattern.Trim();
                    if (String.IsNullOrEmpty(currentPattern))
                    {
                        tracer.Info($"Skipping empty pattern.");
                        continue;
                    }

                    string[] currentPatterns = new string[] { currentPattern };
                    IEnumerable<Func<string, bool>> minimatcherFuncs = MinimatchHelper.GetMinimatchFuncs(
                        currentPatterns,
                        tracer,
                        matchOptions
                    );

                    UpdatePatternsMap(isIncludePattern, items, minimatcherFuncs, ref map);
                }
            }

            // return a filtered version of the original list (preserves order and prevents duplication)
            return ApplyPatternsMapToItems(items, typeofitems, map);
        }

        private string[] ExpandBraces(string pattern, Options matchOptions)
        {
            // Convert slashes on Windows before calling braceExpand(). Unfortunately this means braces cannot
            // be escaped on Windows, this limitation is consistent with current limitations of minimatch (3.0.3).
            tracer.Info($"Expanding braces.");
            string convertedPattern = pattern.Replace("\\", "/");
            return Minimatcher.BraceExpand(convertedPattern, matchOptions).ToArray();
        }

        private void UpdatePatternsMap(bool isIncludePattern, List<string> items, IEnumerable<Func<string, bool>> minimatcherFuncs, ref Hashtable map)
        {
            if (isIncludePattern)
            {
                // Apply the pattern.
                tracer.Info($"Applying include pattern against original list.");
                List<string> matchResults = this.FilterItemsByPatterns(items, minimatcherFuncs);

                // Union the results.
                int matchCount = 0;
                foreach (string matchResult in matchResults)
                {
                    matchCount++;
                    map[matchResult] = Boolean.TrueString;
                }

                tracer.Info($"{matchCount} matches");
            }
            else
            {
                // Apply the pattern.
                tracer.Info($"Applying exclude pattern against original list.");
                List<string> matchResults = this.FilterItemsByPatterns(items, minimatcherFuncs);

                // Subtract the results.
                int matchCount = 0;
                foreach (string matchResult in matchResults)
                {
                    matchCount++;
                    map.Remove(matchResult);
                }

                tracer.Info($"{matchCount} matches");
            }
        }

        private dynamic ApplyPatternsMapToItems(List<string> items, Type typeofitems, Hashtable map)
        {
            string genericType = typeofitems.GenericTypeArguments[0].Name;

            if (genericType == "FileContainerItem")
            {
                List<FileContainerItem> resultItems = new List<FileContainerItem>();
                foreach (string item in items)
                {
                    if (Convert.ToBoolean(map[item]))
                    {
                        resultItems.Add(new FileContainerItem() { Path = item });
                    }
                }

                return resultItems;
            } else if (genericType == "FileInfo")
            {
                List<string> resultItems = new List<string>();
                foreach (string item in items)
                {
                    if (Convert.ToBoolean(map[item]))
                    {
                        resultItems.Add( item );
                    }
                }

                return resultItems;
            }
            
            return items;
        }

        private List<string> FilterItemsByPatterns(List<string> items, IEnumerable<Func<string, bool>> minimatchFuncs)
        {
            List<string> filteredItems = new List<string>();
            foreach (string item in items)
            {
                if (minimatchFuncs.Any(match => match(item)))
                {
                    filteredItems.Add(item);
                }
            }

            return filteredItems;
        }

        // Clones MiniMatch options into separate object
        private Options CloneMiniMatchOptions(Options currentMiniMatchOptions)
        {
            Options clonedMiniMatchOptions = new Options()
            {
                Dot = currentMiniMatchOptions.Dot,
                FlipNegate = currentMiniMatchOptions.FlipNegate,
                MatchBase = currentMiniMatchOptions.MatchBase,
                NoBrace = currentMiniMatchOptions.NoBrace,
                NoCase = currentMiniMatchOptions.NoCase,
                NoComment = currentMiniMatchOptions.NoComment,
                NoExt = currentMiniMatchOptions.NoExt,
                NoGlobStar = currentMiniMatchOptions.NoGlobStar,
                NoNegate = currentMiniMatchOptions.NoNegate,
                NoNull = currentMiniMatchOptions.NoNull,
                IgnoreCase = currentMiniMatchOptions.IgnoreCase,
                AllowWindowsPaths = PlatformUtil.RunningOnWindows
            };
            return clonedMiniMatchOptions;
        }
    }
}
