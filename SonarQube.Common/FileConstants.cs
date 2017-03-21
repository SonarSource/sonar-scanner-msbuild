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
using System.Collections.Generic;
using System.IO;

namespace SonarQube.Common
{
    public static class FileConstants
    {
        /// <summary>
        /// Name of the per-project file that contain information used
        /// during analysis and when generating the sonar-scanner.properties file
        /// </summary>
        public const string ProjectInfoFileName = "ProjectInfo.xml";

        /// <summary>
        /// Name of the file containing analysis configuration settings
        /// </summary>
        public const string ConfigFileName = "SonarQubeAnalysisConfig.xml";

        /// <summary>
        /// Name of the import before target file
        /// </summary>
        public const string ImportBeforeTargetsName = "SonarQube.Integration.ImportBefore.targets";

        /// <summary>
        /// Name of the targets file that contains the integration pieces
        /// </summary>
        public const string IntegrationTargetsName = "SonarQube.Integration.targets";

        /// <summary>
        /// Path to the user specific ImportBefore folders
        /// </summary>
        public static IReadOnlyList<string> ImportBeforeDestinationDirectoryPaths
        {
            get
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                return new string[]
                {
                    Path.Combine(appData, "Microsoft", "MSBuild", "15.0", "Microsoft.Common.targets", "ImportBefore"),
                    Path.Combine(appData, "Microsoft", "MSBuild", "14.0", "Microsoft.Common.targets", "ImportBefore"),
                    Path.Combine(appData, "Microsoft", "MSBuild", "12.0", "Microsoft.Common.targets", "ImportBefore")
                };
            }
        }
    }
}
