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
using SonarQube.TeamBuild.Integration.Interfaces;
using SonarQube.TeamBuild.PostProcessor.Interfaces;
using SonarQube.TeamBuild.PreProcessor;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace SonarQube.Bootstrapper
{
    public class BootstrapperClass
    {
        public const int ErrorCode = 1;
        public const int SuccessCode = 0;

        private readonly IProcessorFactory ProcessorFactory;
        private readonly IBootstrapperSettings BootstrapSettings;
        private readonly ILogger Logger;

        public BootstrapperClass(IProcessorFactory processorFactory, IBootstrapperSettings bootstrapSettings, ILogger logger)
        {
            this.ProcessorFactory = processorFactory;
            this.BootstrapSettings = bootstrapSettings;
            this.Logger = logger;

            Debug.Assert(BootstrapSettings != null, "Bootstrapper settings should not be null");
            Debug.Assert(BootstrapSettings.Phase != AnalysisPhase.Unspecified, "Expecting the processing phase to be specified");
        }

        /// <summary>
        /// Bootstraps a begin or end step, based on the bootstrap settings.
        /// </summary>
        public int Execute()
        {
            int exitCode;
            Logger.Verbosity = BootstrapSettings.LoggingVerbosity;

            AnalysisPhase phase = BootstrapSettings.Phase;
            LogProcessingStarted(phase);

            if (phase == AnalysisPhase.PreProcessing)
            {
                exitCode = PreProcess();
            }
            else
            {
                exitCode = PostProcess();
            }

            LogProcessingCompleted(phase, exitCode);

            return exitCode;
        }

        private int PreProcess()
        {
            Logger.LogInfo(Resources.MSG_PreparingDirectories);
            if (!Utilities.TryEnsureEmptyDirectories(Logger, BootstrapSettings.TempDirectory))
            {
                return ErrorCode;
            }

            CopyDLLs();
            string server = BootstrapSettings.SonarQubeUrl;
            Debug.Assert(!string.IsNullOrWhiteSpace(server), "Not expecting the server URL to be null/empty");
            Logger.LogDebug(Resources.MSG_ServerUrl, server);

            Utilities.LogAssemblyVersion(Logger, typeof(Program).Assembly, Resources.AssemblyDescription);
            Logger.IncludeTimestamp = true;

            ITeamBuildPreProcessor preProcessor = ProcessorFactory.CreatePreProcessor();
            Directory.SetCurrentDirectory(BootstrapSettings.TempDirectory);
            bool success = preProcessor.Execute(BootstrapSettings.ChildCmdLineArgs.ToArray());

            return success ? SuccessCode : ErrorCode;
        }

        private int PostProcess()
        {
            Utilities.LogAssemblyVersion(Logger, typeof(Program).Assembly, Resources.AssemblyDescription);
            Logger.IncludeTimestamp = true;

            if (!Directory.Exists(BootstrapSettings.TempDirectory))
            {
                Logger.LogError(Resources.ERROR_TempDirDoesNotExist);
                return ErrorCode;
            }

            Directory.SetCurrentDirectory(BootstrapSettings.TempDirectory);
            ITeamBuildSettings teamBuildSettings = TeamBuildSettings.GetSettingsFromEnvironment(Logger);
            AnalysisConfig config = GetAnalysisConfig(teamBuildSettings.AnalysisConfigFilePath);

            bool succeeded;
            if (config == null)
            {
                succeeded = false;
            }
            else
            {
                IMSBuildPostProcessor postProcessor = ProcessorFactory.CreatePostProcessor();
                succeeded = postProcessor.Execute(BootstrapSettings.ChildCmdLineArgs.ToArray(), config, teamBuildSettings);
            }

            return succeeded ? SuccessCode : ErrorCode;
        }

        /// <summary>
        /// Copies DLLs needed by the targets file that is loaded by MSBuild to the project's .sonarqube directory
        /// </summary>
        private void CopyDLLs()
        {
            string binDirPath = Path.Combine(BootstrapSettings.TempDirectory, "bin");
            Directory.CreateDirectory(binDirPath);
            string[] dllsToCopy = { "SonarQube.Common.dll", "SonarQube.Integration.Tasks.dll" };

            foreach (string dll in dllsToCopy)
            {
                string dllPath = Path.Combine(BootstrapSettings.ScannerBinaryDirPath, dll);
                File.Copy(dllPath, Path.Combine(binDirPath, dll));
            }
        }

        /// <summary>
        /// Attempts to load the analysis config file. The location of the file is
        /// calculated from TeamBuild-specific environment variables.
        /// Returns null if the required environment variables are not available.
        /// </summary>
        private AnalysisConfig GetAnalysisConfig(string configFilePath)
        {
            AnalysisConfig config = null;

            if (configFilePath != null)
            {
                Debug.Assert(!string.IsNullOrWhiteSpace(configFilePath), "Expecting the analysis config file path to be set");

                if (File.Exists(configFilePath))
                {
                    config = AnalysisConfig.Load(configFilePath);
                }
                else
                {
                    Logger.LogError(Resources.ERROR_ConfigFileNotFound, configFilePath);
                }
            }
            return config;
        }

        private void LogProcessingStarted(AnalysisPhase phase)
        {
            string phaseLabel = phase == AnalysisPhase.PreProcessing ? Resources.PhaseLabel_PreProcessing : Resources.PhaseLabel_PostProcessing;
            Logger.LogInfo(Resources.MSG_ProcessingStarted, phaseLabel);
        }

        private void LogProcessingCompleted(AnalysisPhase phase, int exitCode)
        {
            string phaseLabel = phase == AnalysisPhase.PreProcessing ? Resources.PhaseLabel_PreProcessing : Resources.PhaseLabel_PostProcessing;
            if (exitCode == ProcessRunner.ErrorCode)
            {
                Logger.LogError(Resources.ERROR_ProcessingFailed, phaseLabel, exitCode);
            }
            else
            {
                Logger.LogInfo(Resources.MSG_ProcessingSucceeded, phaseLabel);
            }
        }
    }
}
