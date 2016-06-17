//-----------------------------------------------------------------------
// <copyright file="MockObjectFactory.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarQube.Common;
using SonarQube.TeamBuild.PreProcessor.Interfaces;

namespace SonarQube.TeamBuild.PreProcessor.Tests
{
    internal class MockObjectFactory : IPreprocessorObjectFactory
    {
        private readonly ISonarQubeServer server;
        private readonly IAnalyzerProvider analyzerProvider;
        private readonly ITargetsInstaller targetsInstaller;
        private readonly IRulesetGenerator rulesetGenerator;

        public MockObjectFactory(ISonarQubeServer server)
        {
            this.server = server;
        }

        public MockObjectFactory(ISonarQubeServer server, ITargetsInstaller targetsInstaller, IAnalyzerProvider analyzerProvider, IRulesetGenerator rulesetGenerator)
        {
            this.server = server;
            this.targetsInstaller = targetsInstaller;
            this.analyzerProvider = analyzerProvider;
            this.rulesetGenerator = rulesetGenerator;
        }

        #region PreprocessorObjectFactory methods

        public IAnalyzerProvider CreateRoslynAnalyzerProvider(ILogger logger)
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

        public IRulesetGenerator CreateRulesetGenerator()
        {
            return this.rulesetGenerator;
        }

        #endregion
    }
}
