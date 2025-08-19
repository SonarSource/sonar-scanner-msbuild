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
    internal const string AgentTempDirectory = "AGENT_TEMPDIRECTORY";
    private const string XmlReportFileExtension = "coveragexml";
    private readonly ICoverageReportConverter converter;
    private readonly ILogger logger;
    private readonly IFileWrapper fileWrapper;
    private readonly IDirectoryWrapper directoryWrapper;

    public BuildVNextCoverageReportProcessor(ICoverageReportConverter converter, ILogger logger, IFileWrapper fileWrapper = null, IDirectoryWrapper directoryWrapper = null)
    {
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.converter = converter ?? throw new ArgumentNullException(nameof(converter));
        this.fileWrapper = fileWrapper ?? FileWrapper.Instance;
        this.directoryWrapper = directoryWrapper ?? DirectoryWrapper.Instance;
    }

    public virtual AdditionalProperties ProcessCoverageReports(AnalysisConfig config, IBuildSettings settings, ILogger logger)
    {
        this.logger.LogInfo(Resources.PROC_DIAG_FetchingCoverageReportInfoFromServer);
        string[] vsTestReportsPaths = null;
        string[] vsCoverageXmlReportsPaths = null;
        var trxFilePaths = new TrxFileReader(logger, fileWrapper, directoryWrapper).FindTrxFiles(settings.BuildDirectory);

        var reportsPathsPropertyWritten = false;
        if (config.GetSettingOrDefault(SonarProperties.VsTestReportsPaths, true, null, logger) is null)
        {
            if (trxFilePaths.Any())
            {
                vsTestReportsPaths = trxFilePaths.ToArray();
                reportsPathsPropertyWritten = true;
            }
        }
        else
        {
            this.logger.LogInfo(Resources.TRX_DIAG_SkippingCoverageCheckPropertyProvided);
        }

        var vsCoverageFilePaths = FindVsCoverageFiles(reportsPathsPropertyWritten, trxFilePaths);
        if (vsCoverageFilePaths.Any()
            && TryConvertCoverageReports(vsCoverageFilePaths, out var coverageReportPaths)
            && coverageReportPaths.Any()
            && config.GetSettingOrDefault(SonarProperties.VsCoverageXmlReportsPaths, true, null, logger) is null)
        {
            vsCoverageXmlReportsPaths = coverageReportPaths.ToArray();
        }
        return new(vsTestReportsPaths, vsCoverageXmlReportsPaths);
    }

    internal IEnumerable<string> FindFallbackCoverageFiles()
    {
        logger.LogInfo("Falling back on locating coverage files in the agent temp directory.");

        var agentTempDirectory = CheckAgentTempDirectory();
        if (agentTempDirectory is null)
        {
            return [];
        }

        logger.LogInfo($"Searching for coverage files in {agentTempDirectory}");
        var files = directoryWrapper.GetFiles(agentTempDirectory, "*.coverage", SearchOption.AllDirectories);

        if (files is null || files.Length == 0)
        {
            logger.LogInfo("No coverage files found in the agent temp directory.");
            return [];
        }

        LogDebugFileList("All matching files:", files);

        var fileWithContentHashes = files.Select(x =>
            {
                using var fileStream = fileWrapper.Open(x);
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
        var agentTempDirectory = Environment.GetEnvironmentVariable(AgentTempDirectory);
        if (string.IsNullOrEmpty(agentTempDirectory))
        {
            logger.LogDebug($"Env var {AgentTempDirectory} is not set.");
            return null;
        }

        if (!directoryWrapper.Exists(agentTempDirectory))
        {
            logger.LogDebug($"Calculated location for {AgentTempDirectory} does not exist: {agentTempDirectory}");
            return null;
        }

        return agentTempDirectory;
    }

    private void LogDebugFileList(string headerMessage, string[] files)
    {
        logger.LogDebug($"{headerMessage} count={files.Length}");
        foreach (var file in files)
        {
            logger.LogDebug($"\t{file}");
        }
    }

    private IEnumerable<string> FindVsCoverageFiles(bool reportsPathsPropertyWritten, IEnumerable<string> trxFilePaths)
    {
        var binaryFilePaths = new TrxFileReader(logger, fileWrapper, directoryWrapper).FindCodeCoverageFiles(trxFilePaths);
        if (binaryFilePaths.Any() || reportsPathsPropertyWritten)
        {
            logger.LogDebug(Resources.TRX_DIAG_NotUsingFallback);
            return binaryFilePaths;
        }
        else
        {
            // Fallback to workaround SONARAZDO-179: if the standard searches for .trx/.coverage failed
            // then try the fallback method to find coverage files
            logger.LogInfo(Resources.TRX_DIAG_NoCoverageFilesFound);
            return FindFallbackCoverageFiles();
        }
    }

    private bool TryConvertCoverageReports(IEnumerable<string> vsCoverageFilePaths, out IEnumerable<string> vsCoverageXmlPaths)
    {
        var xmlFileNames = new List<string>();
        foreach (var vsCoverageFilePath in vsCoverageFilePaths)
        {
            var xmlFilePath = Path.ChangeExtension(vsCoverageFilePath, XmlReportFileExtension);
            if (fileWrapper.Exists(xmlFilePath))
            {
                logger.LogInfo(string.Format(Resources.COVXML_DIAG_FileAlreadyExist_NoConversionAttempted, vsCoverageFilePath));
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
