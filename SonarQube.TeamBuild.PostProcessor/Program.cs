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
using SonarScanner.Shim;
using System.Diagnostics;
using System.IO;

namespace SonarQube.TeamBuild.PostProcessor
{
    internal static class Program
    {
        private const int ErrorCode = 1;
        private const int SuccessCode = 0;

        private static int Main(string[] args)
        {
            ConsoleLogger logger = new ConsoleLogger();
            Utilities.LogAssemblyVersion(logger, typeof(Program).Assembly, Resources.AssemblyDescription);
            logger.IncludeTimestamp = true;

            TeamBuildSettings settings = TeamBuildSettings.GetSettingsFromEnvironment(logger);
            Debug.Assert(settings != null, "Settings should not be null");

            AnalysisConfig config = GetAnalysisConfig(settings, logger);

            bool succeeded;
            if (config == null)
            {
                succeeded = false;
            }
            else
            {
                MSBuildPostProcessor postProcessor = new MSBuildPostProcessor(
                    new CoverageReportProcessor(),
                    new SonarScannerWrapper(),
                    new SummaryReportBuilder(),
                    logger,
                    new TargetsUninstaller());

                succeeded = postProcessor.Execute(args, config, settings);
            }

            return succeeded ? SuccessCode : ErrorCode;
        }

        /// <summary>
        /// Attempts to load the analysis config file. The location of the file is
        /// calculated from TeamBuild-specific environment variables.
        /// Returns null if the required environment variables are not available.
        /// </summary>
        private static AnalysisConfig GetAnalysisConfig(TeamBuildSettings teamBuildSettings, ILogger logger)
        {
            AnalysisConfig config = null;

            if (teamBuildSettings != null)
            {
                string configFilePath = teamBuildSettings.AnalysisConfigFilePath;
                Debug.Assert(!string.IsNullOrWhiteSpace(configFilePath), "Expecting the analysis config file path to be set");

                if (File.Exists(configFilePath))
                {
                    config = AnalysisConfig.Load(teamBuildSettings.AnalysisConfigFilePath);
                }
                else
                {
                    logger.LogError(Resources.ERROR_ConfigFileNotFound, configFilePath);
                }
            }
            return config;
        }

    }
}