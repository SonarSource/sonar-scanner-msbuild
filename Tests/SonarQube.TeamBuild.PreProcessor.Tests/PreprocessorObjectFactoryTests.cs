/*
 * SonarQube Scanner for MSBuild
 * Copyright (C) 2015-2017 SonarSource SA and Microsoft Corporation
 * mailto: contact AT sonarsource DOT com
 *
 * Licensed under the MIT License.
 * See LICENSE file in the project root for full license information.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
 * THE SOFTWARE.
 */

using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarQube.Common;
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
            AssertException.Expects<ArgumentNullException>(() => testSubject.CreateRoslynAnalyzerProvider(null));
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
            actual = testSubject.CreateRoslynAnalyzerProvider(logger);
            Assert.IsNotNull(actual);
        }

        [TestMethod]
        public void Factory_InvalidCallSequence_Fails()
        {
            // Arrange
            TestLogger logger = new TestLogger();
            IPreprocessorObjectFactory testSubject = new PreprocessorObjectFactory();

            // 2. Act and assert
            AssertException.Expects<InvalidOperationException>(() => testSubject.CreateRoslynAnalyzerProvider(logger));
        }

        #endregion

        #region Private methods

        private ProcessedArgs CreateValidArguments()
        {
            Common.ListPropertiesProvider cmdLineArgs = new Common.ListPropertiesProvider();
            cmdLineArgs.AddProperty(Common.SonarProperties.HostUrl, "http://foo");

            ProcessedArgs validArgs = new ProcessedArgs("key", "name", "verions", false,
                cmdLineArgs,
                new Common.ListPropertiesProvider(),
                EmptyPropertyProvider.Instance);
            return validArgs;
        }

        #endregion
    }
}
