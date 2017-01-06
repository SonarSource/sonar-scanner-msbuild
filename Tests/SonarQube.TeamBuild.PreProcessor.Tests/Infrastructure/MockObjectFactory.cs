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
