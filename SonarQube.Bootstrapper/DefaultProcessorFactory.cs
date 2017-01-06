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
