//-----------------------------------------------------------------------
// <copyright file="CoverageReportProcessor.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using SonarQube.Common;

namespace SonarQube.TeamBuild.Integration
{

    public class BuildVNextCoverageReportProcessor : CoverageReportProcessorBase
    {
        #region Public methods

        public BuildVNextCoverageReportProcessor()
            : this(new CoverageReportConverter())
        {
        }

        public BuildVNextCoverageReportProcessor(ICoverageReportConverter converter)
            : base(converter)
        {
        }

        #endregion

        #region Overrides

        protected override bool TryGetBinaryReportFile(AnalysisConfig config, TeamBuildSettings settings, ILogger logger, out string binaryFilePath)
        {
            Property vstestReportPath = getVstestReportPathOption(config);

            binaryFilePath = TrxFileReader.LocateCodeCoverageFile(settings.BuildDirectory, logger, vstestReportPath);

            return true; // there aren't currently any conditions under which we'd want to stop processing
        }

        private static Property getVstestReportPathOption(AnalysisConfig config)
        {
            var analysisConfig = config.GetAnalysisSettings(true);
            Property vstestReportPath;
            analysisConfig.TryGetProperty("sonar.cs.vstest.reportsPaths", out vstestReportPath);
            return vstestReportPath;
        }

        #endregion

    }
}
