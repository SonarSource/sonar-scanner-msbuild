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

using SonarQube.Common;
using SonarQube.TeamBuild.PreProcessor.Interfaces;
using System;

namespace SonarQube.TeamBuild.PreProcessor
{
    /// <summary>
    /// Default implementation of the object factory interface that returns the
    /// product implementations of the required classes
    /// </summary>
    /// <remarks>
    /// Note: the factory is stateful and expects objects to be requested in the
    /// order they are used
    /// </remarks>
    public class PreprocessorObjectFactory : IPreprocessorObjectFactory
    {
        /// <summary>
        /// Reference to the SonarQube server to query
        /// </summary>
        /// <remarks>Cannot be constructed at runtime until the command line arguments have been processed.
        /// Once it has been created, it is stored so the factory can use the same instance when
        /// constructing the analyzer provider</remarks>
        private ISonarQubeServer server;

        #region IPreprocessorObjectFactory methods

        public ISonarQubeServer CreateSonarQubeServer(ProcessedArgs args, ILogger logger)
        {
            if (args == null)
            {
                throw new ArgumentNullException("args");
            }
            if (logger == null)
            {
                throw new ArgumentNullException("logger");
            }

            string username = args.GetSetting(SonarProperties.SonarUserName, null);
            string password = args.GetSetting(SonarProperties.SonarPassword, null);
            string hostUrl = args.GetSetting(SonarProperties.HostUrl, null);

            this.server = new SonarWebService(new WebClientDownloader(username, password, logger), hostUrl, logger);
            return server;
        }

        public ITargetsInstaller CreateTargetInstaller()
        {
            return new TargetsInstaller();
        }

        public IAnalyzerProvider CreateRoslynAnalyzerProvider(ILogger logger)
        {
            if (logger == null)
            {
                throw new ArgumentNullException("logger");
            }

            if (this.server == null)
            {
                throw new InvalidOperationException(Resources.FACTORY_InternalError_MissingServer);
            }

            return new Roslyn.RoslynAnalyzerProvider(new Roslyn.EmbeddedAnalyzerInstaller(this.server, logger), logger);
        }

        public IRulesetGenerator CreateRulesetGenerator()
        {
            return new RulesetGenerator();
        }

        #endregion

    }
}
