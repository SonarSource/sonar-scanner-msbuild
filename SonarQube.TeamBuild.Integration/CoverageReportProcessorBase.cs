//-----------------------------------------------------------------------
// <copyright file="CoverageReportProcessorBase.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using SonarQube.Common;
using System;
using System.Diagnostics;
using System.IO;

namespace SonarQube.TeamBuild.Integration
{
    public abstract class CoverageReportProcessorBase : ICoverageReportProcessor
    {
        private const string XmlReportFileExtension = "coveragexml";

        private ICoverageReportConverter converter;

        protected CoverageReportProcessorBase(ICoverageReportConverter converter)
        {
            if (converter == null)
            {
                throw new ArgumentNullException("converter");
            }
            this.converter = converter;
        }

        #region ICoverageReportProcessor interface

        public bool ProcessCoverageReports(AnalysisConfig context, TeamBuildSettings settings, ILogger logger)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }
            if (logger == null)
            {
                throw new ArgumentNullException("logger");
            }

            if (!this.converter.Initialize(logger))
            {
                // If we can't initialize the converter (e.g. we can't find the exe required to do the
                // conversion) there in there isn't any point in downloading the binary reports
                return false;
            }

            // Fetch all of the report URLs
            logger.LogMessage(Resources.PROC_DIAG_FetchingCoverageReportInfoFromServer);

            string binaryFilePath;
            bool continueProcessing = this.TryGetBinaryReportFile(context, settings, logger, out binaryFilePath);

            if (continueProcessing && binaryFilePath != null)
            {
                continueProcessing = ProcessBinaryCodeCoverageReport(binaryFilePath, context, this.converter, logger);
            }

            return continueProcessing;
        }

        protected abstract bool TryGetBinaryReportFile(AnalysisConfig config, TeamBuildSettings settings, ILogger logger, out string binaryFilePath);

        #endregion

        #region Private methods

        private static bool ProcessBinaryCodeCoverageReport(string binaryCoverageFilePath, AnalysisConfig context, ICoverageReportConverter converter, ILogger logger)
        {
            bool success = false;
            string xmlFileName = Path.ChangeExtension(binaryCoverageFilePath, XmlReportFileExtension);

            Debug.Assert(!File.Exists(xmlFileName), "Not expecting a file with the name of the binary-to-XML conversion output to already exist: " + xmlFileName);
            success = converter.ConvertToXml(binaryCoverageFilePath, xmlFileName, logger);

            if (success)
            {
                logger.LogMessage(Resources.PROC_DIAG_UpdatingProjectInfoFiles);
                InsertCoverageAnalysisResults(context.SonarOutputDir, xmlFileName);
            }

            return success;
        }

        /// <summary>
        /// Insert code coverage results information into each projectinfo file
        /// </summary>
        private static void InsertCoverageAnalysisResults(string sonarOutputDir, string coverageFilePath)
        {
            foreach (string projectFolderPath in Directory.GetDirectories(sonarOutputDir))
            {
                ProjectInfo projectInfo = null;

                string projectInfoPath = Path.Combine(projectFolderPath, FileConstants.ProjectInfoFileName);

                if (File.Exists(projectInfoPath))
                {
                    projectInfo = ProjectInfo.Load(projectInfoPath);
                    projectInfo.AnalysisResults.Add(new AnalysisResult() { Id = AnalysisType.VisualStudioCodeCoverage.ToString(), Location = coverageFilePath });
                    projectInfo.Save(projectInfoPath);
                }
            }
        }

        #endregion

    }
}
