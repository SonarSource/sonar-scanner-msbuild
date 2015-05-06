//-----------------------------------------------------------------------
// <copyright file="CoverageReportProcessor.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using SonarQube.Common;
using System;

namespace SonarQube.TeamBuild.Integration
{

    public class BuildVNextCoverageReportProcessor : ICoverageReportProcessor
    {

        private ICoverageReportConverter converter;

        #region Public methods

        public BuildVNextCoverageReportProcessor()
            : this(new CoverageReportConverter())
        {
        }

        public BuildVNextCoverageReportProcessor(ICoverageReportConverter converter)
        {
            if (converter == null)
            {
                throw new ArgumentNullException("converter");
            }
            this.converter = converter;
        }

        #endregion

        #region ICoverageReportProcessor interface

        public bool ProcessCoverageReports(AnalysisConfig context, TeamBuildSettings settings, ILogger logger)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }
            if (settings == null)
            {
                throw new ArgumentNullException("settings");
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

            bool success = true;

            string coverageFilePath = TrxFileReader.LocateCodeCoverageFile(settings.BuildDirectory, logger);
            if (coverageFilePath != null)
            {
                 success = TfsLegacyCoverageReportProcessor.ProcessCodeCoverageReport(coverageFilePath, context, this.converter, logger);
            }

            return success;
        }

        #endregion

    }
}
