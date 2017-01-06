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
using SonarQube.TeamBuild.PreProcessor.Roslyn;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace SonarQube.TeamBuild.PreProcessor.Tests
{
    internal class MockAnalyzerInstaller : IAnalyzerInstaller
    {
        #region Test helpers

        public ISet<string> AssemblyPathsToReturn { get; set; }

        public List<Plugin> SuppliedPlugins = new List<Plugin>();

        #endregion

        #region Checks

        public void AssertExpectedPluginsRequested(IEnumerable<string> plugins)
        {
            foreach(string plugin in plugins)
            {
                AssertExpectedPluginRequested(plugin);
            }
        }

        public void AssertExpectedPluginRequested(string key)
        {
            Assert.IsFalse(this.SuppliedPlugins == null || !this.SuppliedPlugins.Any(), "No plugins have been requested");
            bool found = this.SuppliedPlugins.Any(p => string.Equals(key, p.Key, System.StringComparison.Ordinal));
            Assert.IsTrue(found, "Expected plugin was not requested. Id: {0}", key);
        }

        #endregion

        #region IAnalyzerInstaller methods

        IEnumerable<string> IAnalyzerInstaller.InstallAssemblies(IEnumerable<Plugin> plugins)
        {
            Assert.IsNotNull(plugins, "Supplied list of plugins should not be null");
            foreach(Plugin p in plugins)
            {
                Debug.WriteLine(p.StaticResourceName);
            }
            this.SuppliedPlugins.AddRange(plugins);

            return this.AssemblyPathsToReturn;
        }

        #endregion
    }
}
