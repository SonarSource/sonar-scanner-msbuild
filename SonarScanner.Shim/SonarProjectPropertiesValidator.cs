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

using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using SonarQube.Common;

namespace SonarScanner.Shim
{
    public static class SonarProjectPropertiesValidator
    {
        /// <summary>
        /// Verifies that no sonar-project.properties conflicting with the generated one exists within the project
        /// </summary>
        /// <param name="sonarScannerCwd">Solution folder to check</param>
        /// <param name="projects">MSBuild projects to check, only valid ones will be verified</param>
        /// <param name="onValid">Called when validation succeeded</param>
        /// <param name="onInvalid">Called when validation fails, with the list of folders containing a sonar-project.properties file</param>
        public static void Validate(string sonarScannerCwd, IDictionary<ProjectInfo, ProjectInfoValidity> projects, Action onValid, Action<IList<string>> onInvalid)
        {
            var folders = new List<string>();
            folders.Add(sonarScannerCwd);
            folders.AddRange(projects.Where(p => p.Value == ProjectInfoValidity.Valid).Select(p => Path.GetDirectoryName(p.Key.FullPath)));

            var invalidFolders = folders.Where(f => !Validate(f)).ToList();

            if (!invalidFolders.Any())
            {
                onValid();
            }
            else
            {
                onInvalid(invalidFolders);
            }
        }

        private static bool Validate(string folder)
        {
            return !File.Exists(Path.Combine(folder, "sonar-project.properties"));
        }
    }
}
