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
using SonarQube.TeamBuild.Integration;
using SonarQube.TeamBuild.PreProcessor.Roslyn;
using SonarQube.TeamBuild.PreProcessor.Roslyn.Model;
using System.Collections.Generic;

namespace SonarQube.TeamBuild.PreProcessor.Tests
{
    internal class MockRoslynAnalyzerProvider : IAnalyzerProvider
    {

        #region Test helpers

        public AnalyzerSettings SettingsToReturn { get; set; }

        #endregion

        #region IAnalyzerProvider methods

        AnalyzerSettings IAnalyzerProvider.SetupAnalyzer(TeamBuildSettings settings, IDictionary<string, string> serverSettings, 
            IEnumerable<ActiveRule> activeRules, IEnumerable<string> inactiveRules, string pluginKey)
        {
            Assert.IsNotNull(settings);
            Assert.IsNotNull(serverSettings);
            Assert.IsFalse(string.IsNullOrWhiteSpace(pluginKey));

            return SettingsToReturn;
        }

        #endregion
    }
}
