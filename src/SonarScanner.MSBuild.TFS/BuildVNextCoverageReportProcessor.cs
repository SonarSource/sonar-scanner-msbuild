/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2025 SonarSource SA
 * mailto: info AT sonarsource DOT com
 *
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with this program; if not, write to the Free Software Foundation,
 * Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.
 */

using System.Security.Cryptography;

namespace SonarScanner.MSBuild.TFS;

public class BuildVNextCoverageReportProcessor
{
    private const string XmlReportFileExtension = "coveragexml";
    private readonly ICoverageReportConverter converter;
    private readonly IRuntime runtime;

    public BuildVNextCoverageReportProcessor(ICoverageReportConverter converter, IRuntime runtime)
    {
        this.converter = converter ?? throw new ArgumentNullException(nameof(converter));
        this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
    }

    // ToDo: SCAN4NET-786 Test report discovery is flawed
    // ToDo: SCAN4NET-787 Coverage fallback should be in AzDo Extension
    public virtual AdditionalProperties ProcessCoverageReports(AnalysisConfig config, IBuildSettings settings)
    {
        runtime.Logger.LogInfo(Resources.PROC_DIAG_FetchingCoverageReportInfoFromServer);
        string[] vsTestReportsPaths = null;
        string[] vsCoverageXmlReportsPaths = null;
        var trxFilePaths = new TrxFileReader(runtime.Logger, runtime.File, runtime.Directory).FindTrxFiles(settings.BuildDirectory);

        if (config.GetSettingOrDefault(SonarProperties.VsTestReportsPaths, true, null, runtime.Logger) is null)
        {
            if (trxFilePaths.Any())
            {
                vsTestReportsPaths = trxFilePaths.ToArray();
            }
        }
        else
        {
            runtime.Logger.LogInfo(Resources.TRX_DIAG_SkippingCoverageCheckPropertyProvided);
        }

        var vsCoverageFilePaths = FindVsCoverageFiles(trxFilePaths, disableFallback: vsTestReportsPaths is not null);
        if (vsCoverageFilePaths.Any()
            && TryConvertCoverageReports(vsCoverageFilePaths, out var coverageReportPaths)
            && coverageReportPaths.Any()
            && config.GetSettingOrDefault(SonarProperties.VsCoverageXmlReportsPaths, true, null, runtime.Logger) is null)
        {
            vsCoverageXmlReportsPaths = coverageReportPaths.ToArray();
        }
        return new(vsTestReportsPaths, vsCoverageXmlReportsPaths);
    }

    internal IEnumerable<string> FindFallbackCoverageFiles()
    {
        runtime.Logger.LogInfo("Falling back on locating coverage files in the agent temp directory.");

        var agentTempDirectory = CheckAgentTempDirectory();
        if (agentTempDirectory is null)
        {
            return [];
        }

        runtime.Logger.LogInfo($"Searching for coverage files in {agentTempDirectory}");
        var files = runtime.Directory.GetFiles(agentTempDirectory, "*.coverage", SearchOption.AllDirectories);

        if (files is null || files.Length == 0)
        {
            runtime.Logger.LogInfo("No coverage files found in the agent temp directory.");
            return [];
        }

        LogDebugFileList("All matching files:", files);

        var fileWithContentHashes = files.Select(x =>
            {
                using var fileStream = runtime.File.Open(x);
                using var bufferedStream = new BufferedStream(fileStream);
                using var sha = new SHA256CryptoServiceProvider();
                var contentHash = sha.ComputeHash(bufferedStream);
                return new FileWithContentHash(x, contentHash);
            });

        files = fileWithContentHashes
            .Distinct()
            .Select(x => x.FullFilePath)
            .ToArray();

        LogDebugFileList("Unique coverage files:", files);
        return files;
    }

    internal /* for testing */ string CheckAgentTempDirectory()
    {
        var agentTempDirectory = Environment.GetEnvironmentVariable(EnvironmentVariables.AgentTempDirectory);
        if (string.IsNullOrEmpty(agentTempDirectory))
        {
            runtime.Logger.LogDebug($"Env var {EnvironmentVariables.AgentTempDirectory} is not set.");
            return null;
        }

        if (!runtime.Directory.Exists(agentTempDirectory))
        {
            runtime.Logger.LogDebug($"Calculated location for {EnvironmentVariables.AgentTempDirectory} does not exist: {agentTempDirectory}");
            return null;
        }

        return agentTempDirectory;
    }

    private void LogDebugFileList(string headerMessage, string[] files)
    {
        runtime.Logger.LogDebug($"{headerMessage} count={files.Length}");
        foreach (var file in files)
        {
            runtime.Logger.LogDebug($"\t{file}");
        }
    }

    private IEnumerable<string> FindVsCoverageFiles(IEnumerable<string> trxFilePaths, bool disableFallback)
    {
        var binaryFilePaths = new TrxFileReader(runtime.Logger, runtime.File, runtime.Directory).FindCodeCoverageFiles(trxFilePaths);
        if (binaryFilePaths.Any() || disableFallback)
        {
            runtime.Logger.LogDebug(Resources.TRX_DIAG_NotUsingFallback);
            return binaryFilePaths;
        }
        else
        {
            // Fallback to workaround SONARAZDO-179: if the standard searches for .trx/.coverage failed
            // then try the fallback method to find coverage files
            runtime.Logger.LogInfo(Resources.TRX_DIAG_NoCoverageFilesFound);
            return FindFallbackCoverageFiles();
        }
    }

    private bool TryConvertCoverageReports(IEnumerable<string> vsCoverageFilePaths, out IEnumerable<string> vsCoverageXmlPaths)
    {
        var xmlFileNames = new List<string>();
        foreach (var vsCoverageFilePath in vsCoverageFilePaths)
        {
            var xmlFilePath = Path.ChangeExtension(vsCoverageFilePath, XmlReportFileExtension);
            if (runtime.File.Exists(xmlFilePath))
            {
                runtime.Logger.LogInfo(string.Format(Resources.COVXML_DIAG_FileAlreadyExist_NoConversionAttempted, vsCoverageFilePath));
            }
            else
            {
                if (!converter.ConvertToXml(vsCoverageFilePath, xmlFilePath))
                {
                    vsCoverageXmlPaths = [];
                    return false;
                }
            }
            xmlFileNames.Add(xmlFilePath);
        }
        vsCoverageXmlPaths = xmlFileNames;
        return true;
    }

    internal class FileWithContentHash
    {
        public string FullFilePath { get; }
        public byte[] ContentHash { get; }

        public FileWithContentHash(string fullFilePath, byte[] contentHash)
        {
            FullFilePath = fullFilePath;
            ContentHash = contentHash;
        }

        public override bool Equals(object obj) =>
            obj is FileWithContentHash other
            && ContentHash.SequenceEqual(other.ContentHash);

        // We solely rely on `Equals`
        public override int GetHashCode() => 0;
    }

    public record AdditionalProperties
    {
        public string[] VsTestReportsPaths { get; }
        public string[] VsCoverageXmlReportsPaths { get; }

        public AdditionalProperties(string[] vsTestReportsPaths, string[] vsCoverageXmlReportsPaths)
        {
            VsTestReportsPaths = vsTestReportsPaths;
            VsCoverageXmlReportsPaths = vsCoverageXmlReportsPaths;
        }
    }
}
