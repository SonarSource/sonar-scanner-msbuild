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
using System.IO;

namespace SonarQube.Bootstrapper
{
    public static class Program
    {
        public const int ErrorCode = 1;
        public const int SuccessCode = 0;

        public static int Main(string[] args)
        {
            var logger = new ConsoleLogger();
            Utilities.LogAssemblyVersion(logger, typeof(Program).Assembly, Resources.AssemblyDescription);
            IBootstrapperSettings settings;
            if (!ArgumentProcessor.TryProcessArgs(args, logger, out settings))
            {
                // The argument processor will have logged errors
                return ErrorCode;
            }

            IProcessorFactory processorFactory = new DefaultProcessorFactory(logger);
            BootstrapperClass bootstrapper = new BootstrapperClass(processorFactory, settings, logger);
            return bootstrapper.Execute();
        }

        public interface IProcessorFactory
        {
            IMSBuildPostProcessor createPostProcessor();
            ITeamBuildPreProcessor createPreProcessor();
        }

        public class DefaultProcessorFactory : IProcessorFactory
        {
            private readonly ILogger logger;

            public DefaultProcessorFactory(ILogger logger)
            {
                this.logger = logger;
            }
            public IMSBuildPostProcessor createPostProcessor()
            {
                return new MSBuildPostProcessor(new CoverageReportProcessor(), new SonarScannerWrapper(), new SummaryReportBuilder(), logger);
            }

            public ITeamBuildPreProcessor createPreProcessor()
            {
                IPreprocessorObjectFactory factory = new PreprocessorObjectFactory();
                return new TeamBuildPreProcessor(factory, logger);
            }
        }
    }
}
