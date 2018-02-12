/*
 * SonarQube Scanner for MSBuild
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
using SonarQube.Common;

namespace SonarQube.TeamBuild.Integration
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
    * The TRX file contains information about the results and about any additional additional collectors
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
    public static class TrxFileReader
    {
        /// <summary>
        /// XML namespace of the .trx file
        /// </summary>
        private const string CodeCoverageXmlNamespace = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";

        /// <summary>
        /// The default name of the folder in which test results will be written
        /// </summary>
        private const string TestResultsFolderName = "TestResults";

        #region Public methods

        /// <summary>
        /// Attempts to locate a code coverage file under the specified build directory
        /// </summary>
        /// <returns>The location of the code coverage file, or null if one could not be found</returns>
        /// <remarks>The method uses logic equivalent to that in the VSTest vNext step i.e.
        /// * look for a test results file (*.trx) in a default location under the supplied build directory.
        /// * parse the trx file looking for a code coverage attachment entry
        /// * resolve the attachment entry to an absolute path</remarks>
        public static string LocateCodeCoverageFile(string buildRootDirectory, ILogger logger)
        {
            if (string.IsNullOrWhiteSpace(buildRootDirectory))
            {
                throw new ArgumentNullException("buildRootDirectory");
            }
            if (logger == null)
            {
                throw new ArgumentNullException("logger");
            }

            string coverageFilePath = null;

            var trxFilePath = FindTrxFile(buildRootDirectory, logger);

            if (!string.IsNullOrEmpty(trxFilePath))
            {
                Debug.Assert(File.Exists(trxFilePath), "Expecting the specified trx file to exist: " + trxFilePath);

                coverageFilePath = TryGetCoverageFilePath(trxFilePath, logger);
            }

            return coverageFilePath;
        }

        #endregion Public methods

        #region Private methods

        public static string FindTrxFile(string buildRootDirectory, ILogger logger)
        {
            Debug.Assert(!string.IsNullOrEmpty(buildRootDirectory));
            Debug.Assert(Directory.Exists(buildRootDirectory), "The specified build root directory should exist: " + buildRootDirectory);

            logger.LogInfo(Resources.TRX_DIAG_LocatingTrx);

            var testDirectories = Directory.GetDirectories(buildRootDirectory, TestResultsFolderName, SearchOption.AllDirectories);

            if (testDirectories == null ||
                !testDirectories.Any())
            {
                logger.LogInfo(Resources.TRX_DIAG_TestResultsDirectoryNotFound, buildRootDirectory);
                return null;
            }

            logger.LogInfo(Resources.TRX_DIAG_FolderPaths, string.Join(", ", testDirectories));

            var trxFiles = testDirectories.SelectMany(dir => Directory.GetFiles(dir, "*.trx")).ToArray();

            string trxFilePath = null;

            switch (trxFiles.Length)
            {
                case 0:
                    logger.LogInfo(Resources.TRX_DIAG_NoTestResultsFound);
                    break;

                case 1:
                    trxFilePath = trxFiles[0];
                    logger.LogInfo(Resources.TRX_DIAG_SingleTrxFileFound, trxFilePath);
                    break;

                default:
                    logger.LogWarning(Resources.TRX_WARN_MultipleTrxFilesFound, string.Join(", ", trxFiles));
                    break;
            }

            return trxFilePath;
        }

        private static string TryGetCoverageFilePath(string trxFilePath, ILogger logger)
        {
            Debug.Assert(File.Exists(trxFilePath));

            if (!TryExtractCoverageFilePaths(trxFilePath, logger, out var attachmentUris))
            {
                return null;
            }

            switch (attachmentUris.Count())
            {
                case 0:
                    logger.LogDebug(Resources.TRX_DIAG_NoCodeCoverageInfo);
                    return null;

                case 1:
                    var attachmentName = attachmentUris.First();
                    logger.LogDebug(Resources.TRX_DIAG_SingleCodeCoverageAttachmentFound, attachmentName);

                    var trxDirectoryName = Path.GetDirectoryName(trxFilePath);
                    var trxFileName = Path.GetFileNameWithoutExtension(trxFilePath);

                    var possibleCoveragePath =
                        new[]
                        {
                            attachmentName,
                            Path.Combine(trxDirectoryName, trxFileName, "In", attachmentName),
                            // https://jira.sonarsource.com/browse/SONARMSBRU-361
                            // With VSTest task the coverage file name uses underscore instead of spaces.
                            Path.Combine(trxDirectoryName, trxFileName.Replace(' ', '_'), "In", attachmentName)
                        }
                        .FirstOrDefault(path => File.Exists(path));

                    if (possibleCoveragePath != null)
                    {
                        logger.LogDebug(Resources.TRX_DIAG_AbsoluteTrxPath, possibleCoveragePath);
                    }
                    else
                    {
                        logger.LogWarning(Resources.TRX_WARN_InvalidConstructedCoveragePath, trxFilePath, attachmentName);
                    }

                    return possibleCoveragePath;

                default:
                    logger.LogWarning(Resources.TRX_WARN_MultipleCodeCoverageAttachmentsFound, string.Join(", ", attachmentUris.ToArray()));
                    return null;
            }
        }

        private static bool TryExtractCoverageFilePaths(string trxFilePath, ILogger logger, out IEnumerable<string> coverageFilePaths)
        {
            Debug.Assert(File.Exists(trxFilePath));

            coverageFilePaths = null;
            var continueProcessing = true;

            var runAttachments = new List<string>();
            try
            {
                var doc = new XmlDocument();
                doc.Load(trxFilePath);
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

                coverageFilePaths = runAttachments;
            }
            catch (XmlException ex)
            {
                logger.LogWarning(Resources.TRX_WARN_InvalidTrx, trxFilePath, ex.Message);
                continueProcessing = false;
            }

            return continueProcessing;
        }

        #endregion Private methods
    }
}
