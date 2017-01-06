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
using SonarQube.TeamBuild.Integration;
using SonarQube.TeamBuild.Integration.Interfaces;
using System;
using System.Diagnostics;

namespace SonarQube.TeamBuild.PostProcessor
{
    public class CoverageReportProcessor : ICoverageReportProcessor
    {
        private ICoverageReportProcessor processor;

        private bool initialisedSuccesfully;

        public bool Initialise(AnalysisConfig config, ITeamBuildSettings settings, ILogger logger)
        {
            if (settings == null)
            {
                throw new ArgumentNullException("settings");
            }

            this.TryCreateCoverageReportProcessor(settings);

            this.initialisedSuccesfully = (this.processor != null && this.processor.Initialise(config, settings, logger));
            return this.initialisedSuccesfully;
        }

        public bool ProcessCoverageReports()
        {
            Debug.Assert(this.initialisedSuccesfully, "Initialization failed, cannot process coverage reports");

            return this.processor.ProcessCoverageReports();
        }

        /// <summary>
        /// Factory method to create a coverage report processor for the current build environment.
        /// </summary>
        private void TryCreateCoverageReportProcessor(ITeamBuildSettings settings)
        {
            if (settings.BuildEnvironment == BuildEnvironment.TeamBuild)
            {
                this.processor = new BuildVNextCoverageReportProcessor();
            }
            else if (settings.BuildEnvironment == BuildEnvironment.LegacyTeamBuild
                && !TeamBuildSettings.SkipLegacyCodeCoverageProcessing)
            {
                this.processor = new TfsLegacyCoverageReportProcessor();
            }
        }
    }
}