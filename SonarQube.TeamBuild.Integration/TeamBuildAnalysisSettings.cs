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

namespace SonarQube.TeamBuild.Integration
{
    /// <summary>
    /// Helper class to provide strongly-typed extension methods to access TFS-specific analysis settings
    /// </summary>
    public static class TeamBuildAnalysisSettings
    {
        internal const string TfsUriSettingId = "TfsUri";
        internal const string BuildUriSettingId = "BuildUri";

        #region Public methods

        public static string GetTfsUri(this AnalysisConfig config)
        {
            return config.GetConfigValue(TfsUriSettingId, null);
        }

        public static void SetTfsUri(this AnalysisConfig config, string uri)
        {
            config.SetConfigValue(TfsUriSettingId, uri);
        }

        public static string GetBuildUri(this AnalysisConfig config)
        {
            return config.GetConfigValue(BuildUriSettingId, null);
        }

        public static void SetBuildUri(this AnalysisConfig config, string uri)
        {
            config.SetConfigValue(BuildUriSettingId, uri);
        }

        #endregion
    }
}
