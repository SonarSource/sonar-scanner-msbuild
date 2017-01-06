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
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace SonarScanner.Shim
{
    public static class ProjectLoader
    {
        public static IEnumerable<ProjectInfo> LoadFrom(string dumpFolderPath)
        {
            List<ProjectInfo> result = new List<ProjectInfo>();

            foreach (string projectFolderPath in Directory.GetDirectories(dumpFolderPath))
            {
                var projectInfo = TryGetProjectInfo(projectFolderPath);
                if (projectInfo != null)
                {
                    result.Add(projectInfo);
                }
            }

            return result;
        }

        private static ProjectInfo TryGetProjectInfo(string projectFolderPath)
        {
            ProjectInfo projectInfo = null;

            string projectInfoPath = Path.Combine(projectFolderPath, FileConstants.ProjectInfoFileName);

            if (File.Exists(projectInfoPath))
            {
                projectInfo = ProjectInfo.Load(projectInfoPath);
            }

            return projectInfo;
        }
    }
}
