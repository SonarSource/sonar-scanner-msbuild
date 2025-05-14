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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml;
using SonarScanner.MSBuild.Common;

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
/// Extracts coverage information from a TRX file
/// </summary>
public class TrxFileReader
{
    /// <summary>
    /// XML namespace of the .trx file
    /// </summary>
    private const string CodeCoverageXmlNamespace = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";

    /// <summary>
    /// The default name of the folder in which test results will be written
    /// </summary>
    private const string TestResultsFolderName = "TestResults";

    private readonly ILogger logger;
    private readonly IFileWrapper fileWrapper;
    private readonly IDirectoryWrapper directoryWrapper;

    public TrxFileReader(ILogger logger)
        : this(logger, FileWrapper.Instance, DirectoryWrapper.Instance)
    {
    }

    public TrxFileReader(ILogger logger, IFileWrapper fileWrapper, IDirectoryWrapper directoryWrapper)
    {
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.fileWrapper = fileWrapper;
        this.directoryWrapper = directoryWrapper;
    }

    /// <summary>
    /// Attempts to locate all code coverage files under the specified build directory
    /// </summary>
    /// <returns>The location of all code coverage files, or empty if one could not be found</returns>
    /// <remarks>The method uses logic equivalent to that in the VSTest vNext step i.e.
    /// * look for all test results files (*.trx) in a default location under the supplied build directory.
    /// * parse the trx files looking for all code coverage attachment entries
    /// * resolve all the attachment entries to absolute paths</remarks>
    public IEnumerable<string> FindCodeCoverageFiles(string buildRootDirectory)
    {
        if (string.IsNullOrWhiteSpace(buildRootDirectory))
        {
            throw new ArgumentNullException(nameof(buildRootDirectory));
        }

        var trxFilePaths = FindTrxFiles(buildRootDirectory, false);

        if (!trxFilePaths.Any())
        {
            return Enumerable.Empty<string>();
        }

        Debug.Assert(trxFilePaths.All(fileWrapper.Exists), "Expecting the specified trx files to exist.");

        var coverageReportPaths = GetCoverageAttachments(trxFilePaths)
            .Values
            .SelectMany(x => x)
            .Distinct(StringComparer.OrdinalIgnoreCase) // windows paths
            .ToList();

        if (coverageReportPaths.Count == 0)
        {
            this.logger.LogInfo(Resources.TRX_DIAG_NoCodeCoverageInfo);
        }
        else
        {
            this.logger.LogInfo(Resources.TRX_DIAG_CodeCoverageAttachmentsFound, string.Join(", ", coverageReportPaths));
        }

        return coverageReportPaths;
    }

    public IEnumerable<string> FindTrxFiles(string buildRootDirectory, bool shouldLog = true)
    {
        Debug.Assert(!string.IsNullOrEmpty(buildRootDirectory));
        Debug.Assert(directoryWrapper.Exists(buildRootDirectory),
            "The specified build root directory should exist: " + buildRootDirectory);

        if (shouldLog)
        {
            this.logger.LogInfo(Resources.TRX_DIAG_LocatingTrx);
        }

        var testDirectories = directoryWrapper.GetDirectories(buildRootDirectory, TestResultsFolderName, SearchOption.AllDirectories);

        if (testDirectories == null ||
            !testDirectories.Any())
        {
            if (shouldLog)
            {
                this.logger.LogInfo(Resources.TRX_DIAG_TestResultsDirectoryNotFound, buildRootDirectory);
            }

            return Enumerable.Empty<string>();
        }

        if (shouldLog)
        {
            this.logger.LogInfo(Resources.TRX_DIAG_FolderPaths, string.Join(", ", testDirectories));
        }

        var trxFiles = testDirectories.SelectMany(dir => directoryWrapper.GetFiles(dir, "*.trx")).ToArray();

        if (shouldLog)
        {
            if (trxFiles.Length == 0)
            {
                this.logger.LogInfo(Resources.TRX_DIAG_NoTestResultsFound);
            }
            else
            {
                this.logger.LogInfo(Resources.TRX_DIAG_TrxFilesFound, string.Join(", ", trxFiles));
            }
        }

        return trxFiles;
    }

    private Dictionary<string, List<string>> GetCoverageAttachments(IEnumerable<string> trxFilePaths)
    {
        var attachmentsPerTrx = new Dictionary<string, List<string>>();

        foreach (var trxPath in trxFilePaths)
        {
            attachmentsPerTrx[trxPath] = new List<string>();

            try
            {
                var doc = new XmlDocument();
                doc.Load(fileWrapper.Open(trxPath));
                var nsmgr = new XmlNamespaceManager(doc.NameTable);
                nsmgr.AddNamespace("x", CodeCoverageXmlNamespace);

                var attachmentNodes = doc.SelectNodes("/x:TestRun/x:ResultSummary/x:CollectorDataEntries/x:Collector[@uri='datacollector://microsoft/CodeCoverage/2.0']/x:UriAttachments/x:UriAttachment/x:A", nsmgr);
                var runDeploymentRootNode = doc.SelectSingleNode("/x:TestRun/x:TestSettings[@name='default']/x:Deployment", nsmgr);
                XmlAttribute runDeploymentRoot = null;
                if (runDeploymentRootNode is not null)
                {
                    runDeploymentRoot = runDeploymentRootNode.Attributes["runDeploymentRoot"];
                }

                foreach (XmlNode attachmentNode in attachmentNodes)
                {
                    var att = attachmentNode.Attributes["href"];
                    if (att == null || att.Value == null)
                    {
                        continue;
                    }

                    var coverageFullPath = TryFindCoverageFileFromUri(trxPath, att.Value, runDeploymentRoot?.Value);
                    if (coverageFullPath != null)
                    {
                        attachmentsPerTrx[trxPath].Add(coverageFullPath);
                    }
                }
            }
            catch (XmlException ex)
            {
                this.logger.LogWarning(Resources.TRX_WARN_InvalidTrx, trxPath, ex.Message);
                return new Dictionary<string, List<string>>();
            }
        }

        return attachmentsPerTrx;
    }

    private string TryFindCoverageFileFromUri(string trx, string attachmentUri, string runDeploymentRoot)
    {
        var trxDirectoryName = Path.GetDirectoryName(trx);
        var trxFileName = Path.GetFileNameWithoutExtension(trx);

        var possibleCoveragePaths = new List<string>();
        possibleCoveragePaths.Add(attachmentUri);
        possibleCoveragePaths.Add(Path.Combine(trxDirectoryName, trxFileName, "In", attachmentUri));
        // https://jira.sonarsource.com/browse/SONARMSBRU-361
        // With VSTest task the coverage file name uses underscore instead of spaces.
        possibleCoveragePaths.Add(Path.Combine(trxDirectoryName, trxFileName.Replace(' ', '_'), "In", attachmentUri));
        if (runDeploymentRoot != null)
        {
            possibleCoveragePaths.Add(Path.Combine(trxDirectoryName, runDeploymentRoot, "In", attachmentUri));
        }
        var firstFoundCoveragePath = possibleCoveragePaths.FirstOrDefault(path => this.fileWrapper.Exists(path));

        if (firstFoundCoveragePath != null)
        {
            this.logger.LogDebug(Resources.TRX_DIAG_AbsoluteTrxPath, firstFoundCoveragePath);
            return firstFoundCoveragePath;
        }
        else
        {
            this.logger.LogWarning(Resources.TRX_WARN_InvalidConstructedCoveragePath,
                string.Join(", ", possibleCoveragePaths), trx);
            return null;
        }
    }
}
