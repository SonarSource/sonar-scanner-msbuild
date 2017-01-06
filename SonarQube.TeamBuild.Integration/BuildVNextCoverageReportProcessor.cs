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
using SonarQube.TeamBuild.Integration.Interfaces;

namespace SonarQube.TeamBuild.Integration
{

    public class BuildVNextCoverageReportProcessor : CoverageReportProcessorBase
    {
        #region Public methods

        public BuildVNextCoverageReportProcessor()
            : this(new CoverageReportConverter())
        {
        }

        public BuildVNextCoverageReportProcessor(ICoverageReportConverter converter)
            : base(converter)
        {
        }

        #endregion

        #region Overrides
        
        protected override bool TryGetBinaryReportFile(AnalysisConfig config, ITeamBuildSettings settings, ILogger logger, out string binaryFilePath)
        {
            binaryFilePath = TrxFileReader.LocateCodeCoverageFile(settings.BuildDirectory, logger);

            return true; // there aren't currently any conditions under which we'd want to stop processing
        }

        #endregion

    }
}
