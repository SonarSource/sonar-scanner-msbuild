/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2025 SonarSource Sàrl
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

using System.Xml;

namespace SonarScanner.MSBuild.TFS;

/* Build vNext code coverage processing:
* -------------------------------------
* We're assuming the standard Visual Studio Test step is being used.
*
* In the UI the user can specify:
*  - the Run Settings File to use
*  - the edition of VS to use
*  - whether Code Coverage is enabled
*
* VSTest power shell script
* -------------------------
* The power shell script calls the "Invoke-VSTest" cmdlet, followed by the "Invoke_ResultPublisher" cmdlet.
* One of the inputs to the "Invoke_ResultPublisher" cmdlet is the TRX file that was created by the tests.
* The TRX file contains information about the results and about any additional collectors
* that were executed, including the code coverage processor.
*
* The script assumes the TRX file is contained in the test results directory, which by default will be in
* "[Agent.BuildDirectory]\TestResults".
* If the user has specified a run settings file then it's possible that the test results
* directory will have changed. However, the script doesn't seem to handle this case.
*
*
*/

/// <summary>
/// Extracts coverage information from a TRX file.
/// </summary>
public class TrxFileReader
{
    /// <summary>
    /// XML namespace of the .trx file.
    /// </summary>
    private const string CodeCoverageXmlNamespace = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";

    /// <summary>
    /// The default name of the folder in which test results will be written.
    /// </summary>
    private const string TestResultsFolderName = "TestResults";

    private readonly IRuntime runtime;

    public TrxFileReader(IRuntime runtime) =>
        this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));

    /// <summary>
    /// Attempts to locate all code coverage files under the specified build directory.
    /// </summary>
    /// <returns>The location of all code coverage files, or empty if one could not be found.</returns>
    /// <remarks>The method uses logic equivalent to that in the VSTest vNext step i.e.
    /// * look for all test results files (*.trx) in a default location under the supplied build directory.
    /// * parse the trx files looking for all code coverage attachment entries
    /// * resolve all the attachment entries to absolute paths.</remarks>
    public IEnumerable<string> FindCodeCoverageFiles(IEnumerable<string> trxFilePaths)
    {
        Debug.Assert(trxFilePaths.All(runtime.File.Exists), "Expecting the specified trx files to exist.");

        var coverageReportPaths = CoverageAttachments(trxFilePaths)
            .Values
            .SelectMany(x => x)
            .Distinct(StringComparer.OrdinalIgnoreCase) // windows paths
            .ToList();

        if (coverageReportPaths.Count == 0)
        {
            runtime.LogInfo(Resources.TRX_DIAG_NoCodeCoverageInfo);
        }
        else
        {
            runtime.LogInfo(Resources.TRX_DIAG_CodeCoverageAttachmentsFound, string.Join(", ", coverageReportPaths));
        }

        return coverageReportPaths;
    }

    public IEnumerable<string> FindTrxFiles(string buildRootDirectory)
    {
        Debug.Assert(!string.IsNullOrEmpty(buildRootDirectory), $"{nameof(buildRootDirectory)} should not be 'null'");
        Debug.Assert(runtime.Directory.Exists(buildRootDirectory), "The specified build root directory should exist: " + buildRootDirectory);

        runtime.LogInfo(Resources.TRX_DIAG_LocatingTrx);

        var testDirectories = runtime.Directory.GetDirectories(buildRootDirectory, TestResultsFolderName, SearchOption.AllDirectories);

        if (testDirectories is null || !testDirectories.Any())
        {
            runtime.LogInfo(Resources.TRX_DIAG_TestResultsDirectoryNotFound, buildRootDirectory);

            return [];
        }

        runtime.LogInfo(Resources.TRX_DIAG_FolderPaths, string.Join(", ", testDirectories));

        var trxFiles = testDirectories.SelectMany(x => runtime.Directory.GetFiles(x, "*.trx")).ToArray();

        if (trxFiles.Length == 0)
        {
            runtime.LogInfo(Resources.TRX_DIAG_NoTestResultsFound);
        }
        else
        {
            runtime.LogInfo(Resources.TRX_DIAG_TrxFilesFound, string.Join(", ", trxFiles));
        }

        return trxFiles;
    }

    private Dictionary<string, List<string>> CoverageAttachments(IEnumerable<string> trxFilePaths)
    {
        var attachmentsPerTrx = new Dictionary<string, List<string>>();

        foreach (var trxPath in trxFilePaths)
        {
            attachmentsPerTrx[trxPath] = [];

            try
            {
                attachmentsPerTrx[trxPath] = TryFindCoverageFiles(trxPath).ToList();
            }
            catch (XmlException ex)
            {
                runtime.LogWarning(Resources.TRX_WARN_InvalidTrx, trxPath, ex.Message);
                return [];
            }
        }

        return attachmentsPerTrx;
    }

    private IEnumerable<string> TryFindCoverageFiles(string trxPath)
    {
        var doc = new XmlDocument();
        doc.Load(runtime.File.Open(trxPath));
        var nsmgr = new XmlNamespaceManager(doc.NameTable);
        nsmgr.AddNamespace("x", CodeCoverageXmlNamespace);

        // when i generate .trx file after switching to Microsoft.Test.Platform under .NET 10 the entry looks like
        // <Collector agentName="WORKSPA-NBFDOTR" uri="datacollector://TestingPlatformCoverageDynamicTestSessionLifetimeHandler/1.0.0" collectorDisplayName="Code Coverage Process Monitor">
        // instead of
        // <Collector agentName="WORKSPA-NBFDOTR" uri="datacollector://microsoft/CodeCoverage/2.0" collectorDisplayName="Code Coverage">
        // so i think this should handle either style
        var attachmentNodes = doc.SelectNodes("/x:TestRun/x:ResultSummary/x:CollectorDataEntries/x:Collector[@uri='datacollector://microsoft/CodeCoverage/2.0']/x:UriAttachments/x:UriAttachment/x:A", nsmgr);
        // The deployment root is used in the path for the attachments. It is read by Microsoft's implementation here:
        // https://github.com/microsoft/testfx/blob/718e38b4558d39afde8bd4a9e6b3566336867c67/src/Platform/Microsoft.Testing.Extensions.TrxReport/TrxReportEngine.cs#L241-L250
        var deploymentRoot = doc.SelectSingleNode("/x:TestRun/x:TestSettings/x:Deployment", nsmgr) is { } runDeploymentRootNode
                                && runDeploymentRootNode.Attributes["runDeploymentRoot"] is { Value: { } runDeploymentRootAttributeValue }
                                    ? runDeploymentRootAttributeValue
                                    : string.Empty;

        foreach (XmlNode attachmentNode in attachmentNodes)
        {
            if (attachmentNode.Attributes["href"]?.Value is { } hrefValue
                && TryFindCoverageFileFromUri(trxPath, hrefValue, deploymentRoot) is { } coverageFullPath)
            {
                yield return coverageFullPath;
            }
        }
    }

    private string TryFindCoverageFileFromUri(string trx, string attachmentUri, string deploymentRoot)
    {
        var trxDirectoryName = Path.GetDirectoryName(trx);
        var trxFileName = Path.GetFileNameWithoutExtension(trx);

        IReadOnlyCollection<string> possibleCoveragePaths =
        [
            attachmentUri,
            Path.Combine(trxDirectoryName, trxFileName, "In", attachmentUri),
            // https://jira.sonarsource.com/browse/SONARMSBRU-361
            // With VSTest task the coverage file name uses underscore instead of spaces.
            Path.Combine(trxDirectoryName, trxFileName.Replace(' ', '_'), "In", attachmentUri),
            // The deployment root, specified ion the trx header, is used in the path for the attachments.
            // https://github.com/microsoft/testfx/blob/718e38b4558d39afde8bd4a9e6b3566336867c67/src/Platform/Microsoft.Testing.Extensions.TrxReport/TrxReportEngine.cs#L378
            .. string.IsNullOrEmpty(deploymentRoot) ? [] : (IReadOnlyCollection<string>)[Path.Combine(trxDirectoryName, deploymentRoot, "In", attachmentUri)],
        ];
        var firstFoundCoveragePath = possibleCoveragePaths.FirstOrDefault(x => runtime.File.Exists(x));

        if (firstFoundCoveragePath is null)
        {
            runtime.LogWarning(Resources.TRX_WARN_InvalidConstructedCoveragePath, string.Join(", ", possibleCoveragePaths), trx);
            return null;
        }
        else
        {
            runtime.LogDebug(Resources.TRX_DIAG_AbsoluteTrxPath, firstFoundCoveragePath);
            return firstFoundCoveragePath;
        }
    }
}
