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

using SonarQube.TeamBuild.Integration;
using TestUtilities;

namespace SonarQube.TeamBuild.PreProcessor.Tests
{
    internal static class PreprocessTestUtils
    {
        /// <summary>
        /// Creates and returns an environment scope configured as if it
        /// is not running under TeamBuild
        /// </summary>
        public static EnvironmentVariableScope CreateValidNonTeamBuildScope()
        {
            EnvironmentVariableScope scope = new EnvironmentVariableScope();
            scope.SetVariable(TeamBuildSettings.EnvironmentVariables.IsInTeamFoundationBuild, "false");

            scope.SetVariable(TeamBuildSettings.EnvironmentVariables.TfsCollectionUri_Legacy, null);
            scope.SetVariable(TeamBuildSettings.EnvironmentVariables.TfsCollectionUri_TFS2015, null);

            scope.SetVariable(TeamBuildSettings.EnvironmentVariables.BuildUri_Legacy, null);
            scope.SetVariable(TeamBuildSettings.EnvironmentVariables.BuildUri_TFS2015, null);

            return scope;
        }
    }
}
