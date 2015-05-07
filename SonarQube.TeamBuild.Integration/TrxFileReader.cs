//-----------------------------------------------------------------------
// <copyright file="TrxFileReader.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using SonarQube.Common;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml;

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

            string trxFilePath = FindTrxFile(buildRootDirectory, logger);

            if (!string.IsNullOrEmpty(trxFilePath))
            {
                Debug.Assert(File.Exists(trxFilePath), "Expecting the specified trx file to exist: " + trxFilePath);

                coverageFilePath = TryGetCoverageFilePath(trxFilePath, logger);
            }

            return coverageFilePath;
        }

        #endregion

        #region Private methods

        private static string FindTrxFile(string buildRootDirectory, ILogger logger)
        {
            Debug.Assert(!string.IsNullOrEmpty(buildRootDirectory));
            Debug.Assert(Directory.Exists(buildRootDirectory), "The specified build root directory should exist: " + buildRootDirectory);

            logger.LogMessage(Resources.TRX_DIAG_LocatingTrx);
            string trxFilePath = null;

            string testResultsPath = Path.Combine(buildRootDirectory, TestResultsFolderName);

            if (Directory.Exists(testResultsPath))
            {
                string[] trxFiles = Directory.GetFiles(testResultsPath, "*.trx");

                switch (trxFiles.Length)
                {
                    case 0:
                        logger.LogMessage(Resources.TRX_DIAG_NoTestResultsFound);
                        break;

                    case 1:
                        trxFilePath = trxFiles[0];
                        logger.LogMessage(Resources.TRX_DIAG_SingleTrxFileFound, trxFilePath);
                        break;

                    default:
                        logger.LogWarning(Resources.TRX_WARN_MultipleTrxFilesFound, string.Join(", ", trxFiles));
                        break;
                }
            }
            else
            {
                logger.LogMessage(Resources.TRX_DIAG_TestResultsDirectoryNotFound, testResultsPath);
            }
            return trxFilePath;
        }

        private static string TryGetCoverageFilePath(string trxFilePath, ILogger logger)
        {
            Debug.Assert(File.Exists(trxFilePath));

            string coverageFilePath = null;

            IEnumerable<string> attachmentUris;
            if (TryExtractCoverageFilePaths(trxFilePath, logger, out attachmentUris))
            {
                switch (attachmentUris.Count())
                {
                    case 0:
                        logger.LogMessage(Resources.TRX_DIAG_NoCodeCoverageInfo);
                        break;
                    case 1:
                        coverageFilePath = attachmentUris.First();
                        logger.LogMessage(Resources.TRX_DIAG_SingleCodeCoverageAttachmentFound, coverageFilePath);

                        if (!Path.IsPathRooted(coverageFilePath))
                        {
                            coverageFilePath = Path.Combine(Path.GetDirectoryName(trxFilePath), Path.GetFileNameWithoutExtension(trxFilePath), "In", coverageFilePath);
                            logger.LogMessage(Resources.TRX_DIAG_AbsoluteTrxPath, coverageFilePath);
                        }

                        break;
                    default:
                        logger.LogWarning(Resources.TRX_WARN_MultipleCodeCoverageAttachmentsFound, string.Join(", ", attachmentUris.ToArray()));
                        break;
                }
            }

            return coverageFilePath;
        }

        private static bool TryExtractCoverageFilePaths(string trxFilePath, ILogger logger, out IEnumerable<string> coverageFilePaths)
        {
            Debug.Assert(File.Exists(trxFilePath));

            coverageFilePaths = null;
            bool continueProcessing = true;
            
            List<string> runAttachments = new List<string>();
            try
            {
                XmlDocument doc = new XmlDocument();
                doc.Load(trxFilePath);
                XmlNamespaceManager nsmgr = new XmlNamespaceManager(doc.NameTable);
                nsmgr.AddNamespace("x", CodeCoverageXmlNamespace);

                XmlNodeList attachmentNodes = doc.SelectNodes("/x:TestRun/x:ResultSummary/x:CollectorDataEntries/x:Collector[@uri='datacollector://microsoft/CodeCoverage/2.0']/x:UriAttachments/x:UriAttachment/x:A", nsmgr);
               
                foreach (XmlNode attachmentNode in attachmentNodes)
                {
                    XmlAttribute att = attachmentNode.Attributes["href"];
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

        #endregion
    }
}