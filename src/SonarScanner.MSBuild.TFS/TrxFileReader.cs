/*
 * SonarScanner for MSBuild
 * Copyright (C) 2016-2018 SonarSource SA
 * mailto:info AT sonarsource DOT com
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

namespace SonarScanner.MSBuild.TFS
{
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

        public TrxFileReader(ILogger logger)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Attempts to locate a code coverage file under the specified build directory
        /// </summary>
        /// <returns>The location of the code coverage file, or null if one could not be found</returns>
        /// <remarks>The method uses logic equivalent to that in the VSTest vNext step i.e.
        /// * look for a test results file (*.trx) in a default location under the supplied build directory.
        /// * parse the trx file looking for a code coverage attachment entry
        /// * resolve the attachment entry to an absolute path</remarks>
        public IEnumerable<string> FindCodeCoverageFiles(string buildRootDirectory)
        {
            if (string.IsNullOrWhiteSpace(buildRootDirectory))
            {
                throw new ArgumentNullException(nameof(buildRootDirectory));
            }

            var trxFilePaths = FindTrxFiles(buildRootDirectory);

            if (!trxFilePaths.Any())
            {
                return Enumerable.Empty<string>();
            }

            Debug.Assert(trxFilePaths.All(File.Exists), "Expecting the specified trx files to exist.");

            if (!TryExtractCoverageFilePaths(trxFilePaths, out var attachmentUris))
            {
                return Enumerable.Empty<string>();
            }

            if (!attachmentUris.Any())
            {
                this.logger.LogDebug(Resources.TRX_DIAG_NoCodeCoverageInfo);
                return Enumerable.Empty<string>();
            }

            this.logger.LogDebug(Resources.TRX_DIAG_CodeCoverageAttachmentsFound, string.Join(", ", attachmentUris));

            var potentialCoverageFiles = GetPotentialCoverageFiles(trxFilePaths, attachmentUris)
                .Distinct()
                .Where(File.Exists)
                .ToList();

            if (potentialCoverageFiles.Count == 0)
            {
                this.logger.LogWarning(Resources.TRX_WARN_CoverageAttachmentsNotFound);
            }

            return potentialCoverageFiles;
        }

        private IEnumerable<string> GetPotentialCoverageFiles(IEnumerable<string> trxFilePaths, IEnumerable<string> coverageAttachmentPaths)
        {
            foreach (var trxPath in trxFilePaths)
            {
                var trxDirectoryName = Path.GetDirectoryName(trxPath);
                var trxFileName = Path.GetFileNameWithoutExtension(trxPath);

                foreach (var coveragePath in coverageAttachmentPaths)
                {
                    yield return coveragePath;
                    yield return Path.Combine(trxDirectoryName, trxFileName, "In", coveragePath);
                    // https://jira.sonarsource.com/browse/SONARMSBRU-361
                    // With VSTest task the coverage file name uses underscore instead of spaces.
                    yield return Path.Combine(trxDirectoryName, trxFileName.Replace(' ', '_'), "In", coveragePath);
                }
            }
        }

        public IEnumerable<string> FindTrxFiles(string buildRootDirectory)
        {
            Debug.Assert(!string.IsNullOrEmpty(buildRootDirectory));
            Debug.Assert(Directory.Exists(buildRootDirectory), "The specified build root directory should exist: " + buildRootDirectory);

            this.logger.LogInfo(Resources.TRX_DIAG_LocatingTrx);

            var testDirectories = Directory.GetDirectories(buildRootDirectory, TestResultsFolderName, SearchOption.AllDirectories);

            if (testDirectories == null ||
                !testDirectories.Any())
            {
                this.logger.LogInfo(Resources.TRX_DIAG_TestResultsDirectoryNotFound, buildRootDirectory);
                return Enumerable.Empty<string>();
            }

            this.logger.LogInfo(Resources.TRX_DIAG_FolderPaths, string.Join(", ", testDirectories));

            var trxFiles = testDirectories.SelectMany(dir => Directory.GetFiles(dir, "*.trx")).ToArray();

            if (trxFiles.Length == 0)
            {
                this.logger.LogInfo(Resources.TRX_DIAG_NoTestResultsFound);
            }
            else
            {
                this.logger.LogInfo(Resources.TRX_DIAG_TrxFilesFound, string.Join(", ", trxFiles));
            }

            return trxFiles;
        }

        private bool TryExtractCoverageFilePaths(IEnumerable<string> trxFilePaths, out IEnumerable<string> coverageFilePaths)
        {
            var runAttachments = new List<string>();

            foreach (var trxPath in trxFilePaths)
            {
                try
                {
                    var doc = new XmlDocument();
                    doc.Load(trxPath);
                    var nsmgr = new XmlNamespaceManager(doc.NameTable);
                    nsmgr.AddNamespace("x", CodeCoverageXmlNamespace);

                    var attachmentNodes = doc.SelectNodes("/x:TestRun/x:ResultSummary/x:CollectorDataEntries/x:Collector[@uri='datacollector://microsoft/CodeCoverage/2.0']/x:UriAttachments/x:UriAttachment/x:A", nsmgr);

                    foreach (XmlNode attachmentNode in attachmentNodes)
                    {
                        var att = attachmentNode.Attributes["href"];
                        if (att != null && att.Value != null)
                        {
                            runAttachments.Add(att.Value);
                        }
                    }
                }
                catch (XmlException ex)
                {
                    this.logger.LogWarning(Resources.TRX_WARN_InvalidTrx, trxFilePaths, ex.Message);
                    coverageFilePaths = null;
                    return false;
                }
            }

            coverageFilePaths = runAttachments;
            return true;
        }
    }
}
