//-----------------------------------------------------------------------
// <copyright file="MockObjectFactory.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarQube.Common;

namespace SonarQube.TeamBuild.PreProcessor.Tests
{
    internal class MockObjectFactory : IPreprocessorObjectFactory
    {
        private readonly ISonarQubeServer server;
        private readonly IAnalyzerProvider analyzerProvider;
        private readonly ITargetsInstaller targetsInstaller;
        private readonly IBuildWrapperInstaller buildWrapperInstaller;

        public MockObjectFactory(ISonarQubeServer server)
        {
            this.server = server;
        }

        public MockObjectFactory(ISonarQubeServer server, ITargetsInstaller targetsInstaller, IAnalyzerProvider analyzerProvider, IBuildWrapperInstaller buildWrapperInstaller)
        {
            this.server = server;
            this.targetsInstaller = targetsInstaller;
            this.analyzerProvider = analyzerProvider;
            this.buildWrapperInstaller = buildWrapperInstaller;
        }

        #region PreprocessorObjectFactory methods

        public IAnalyzerProvider CreateAnalyzerProvider(ILogger logger)
        {
            Assert.IsNotNull(logger);
            return this.analyzerProvider;
        }

        public ISonarQubeServer CreateSonarQubeServer(ProcessedArgs args, ILogger logger)
        {
            Assert.IsNotNull(args);
            Assert.IsNotNull(logger);

            return this.server;
        }

        public ITargetsInstaller CreateTargetInstaller()
        {
            return this.targetsInstaller;
        }

        public IBuildWrapperInstaller CreateBuildWrapperInstaller(ILogger logger)
        {
            Assert.IsNotNull(logger);
            return this.buildWrapperInstaller;
        }


        #endregion
    }
}
