//-----------------------------------------------------------------------
// <copyright file="PreprocessorObjectFactoryTests.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using TestUtilities;

namespace SonarQube.TeamBuild.PreProcessor.Tests
{
    [TestClass]
    public class PreprocessorObjectFactoryTests
    {
        public TestContext TestContext { get; set; }

        #region Tests

        [TestMethod]
        public void Factory_ThrowsOnInvalidInput()
        {
            // Arrange
            TestLogger logger = new TestLogger();
            ProcessedArgs validArgs = CreateValidArguments();
            IPreprocessorObjectFactory testSubject = new PreprocessorObjectFactory();
 
            // 1. CreateSonarQubeServer method
            AssertException.Expects<ArgumentNullException>(() => testSubject.CreateSonarQubeServer(null, logger));
            AssertException.Expects<ArgumentNullException>(() => testSubject.CreateSonarQubeServer(validArgs, null));

            // 2. CreateAnalyzerProvider method
            AssertException.Expects<ArgumentNullException>(() => testSubject.CreateAnalyzerProvider(null));
        }

        [TestMethod]
        public void Factory_ValidCallSequence_ValidObjectReturned()
        {
            // Arrange
            TestLogger logger = new TestLogger();
            ProcessedArgs validArgs = CreateValidArguments();
            IPreprocessorObjectFactory testSubject = new PreprocessorObjectFactory();

            // 1. Create the SonarQube server...
            object actual = testSubject.CreateSonarQubeServer(validArgs, logger);
            Assert.IsNotNull(actual);

            // 2. Now create the targets provider
            actual = testSubject.CreateTargetInstaller();
            Assert.IsNotNull(actual);

            // 3. Now create the analyzer provider
            actual = testSubject.CreateAnalyzerProvider(logger);
            Assert.IsNotNull(actual);
        }

        [TestMethod]
        public void Factory_InvalidCallSequence_Fails()
        {
            // Arrange
            TestLogger logger = new TestLogger();
            IPreprocessorObjectFactory testSubject = new PreprocessorObjectFactory();

            // 2. Act and assert
            AssertException.Expects<InvalidOperationException>(() => testSubject.CreateAnalyzerProvider(logger));
        }

        #endregion

        #region Private methods

        private ProcessedArgs CreateValidArguments()
        {
            Common.ListPropertiesProvider cmdLineArgs = new Common.ListPropertiesProvider();
            cmdLineArgs.AddProperty(Common.SonarProperties.HostUrl, "http://foo");

            ProcessedArgs validArgs = new ProcessedArgs("key", "name", "verions", false,
                cmdLineArgs,
                new Common.ListPropertiesProvider());
            return validArgs;
        }

        #endregion
    }
}
