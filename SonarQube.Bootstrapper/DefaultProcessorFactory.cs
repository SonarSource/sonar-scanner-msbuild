//-----------------------------------------------------------------------
// <copyright file="Program.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using SonarQube.Common;
using SonarQube.TeamBuild.PostProcessor;
using SonarQube.TeamBuild.PostProcessor.Interfaces;
using SonarQube.TeamBuild.PreProcessor;
using SonarScanner.Shim;

namespace SonarQube.Bootstrapper
{
    public class DefaultProcessorFactory : IProcessorFactory
    {
        private readonly ILogger logger;

        public DefaultProcessorFactory(ILogger logger)
        {
            this.logger = logger;
        }
        public IMSBuildPostProcessor CreatePostProcessor()
        {
            return new MSBuildPostProcessor(
                new CoverageReportProcessor(),
                new SonarScannerWrapper(),
                new SummaryReportBuilder(),
                logger,
                new TargetsUninstaller());
        }

        public ITeamBuildPreProcessor CreatePreProcessor()
        {
            IPreprocessorObjectFactory factory = new PreprocessorObjectFactory();
            return new TeamBuildPreProcessor(factory, logger);
        }
    }
}
