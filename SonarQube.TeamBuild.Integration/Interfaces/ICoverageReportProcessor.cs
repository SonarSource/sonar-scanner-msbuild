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
    public interface ICoverageReportProcessor
    {
        /// <summary>
        /// Initialises the converter
        /// </summary>
        /// <returns>Operation success</returns>
        bool Initialise(AnalysisConfig config, ITeamBuildSettings settings, ILogger logger);

        /// <summary>
        /// Locate, download and convert the code coverage report
        /// </summary>
        /// <returns>Operation success</returns>
        bool ProcessCoverageReports();
    }
}
