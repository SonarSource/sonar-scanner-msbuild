/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2023 SonarSource SA
 * mailto: info AT sonarsource DOT com
 *
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with this program; if not, write to the Free Software Foundation,
 * Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.Setup.Configuration;
using Microsoft.Win32;
using SonarScanner.MSBuild.Common;

namespace SonarScanner.MSBuild.TFS.Classic
{
    public class BinaryToXmlCoverageReportConverter : ICoverageReportConverter
    {
        private const int ConversionTimeoutInMs = 60000;
        private readonly IVisualStudioSetupConfigurationFactory setupConfigurationFactory;
        private readonly ILogger logger;
        private readonly AnalysisConfig config;

        /// <summary>
        /// Registry containing information about installed VS versions
        /// </summary>
        private const string VisualStudioRegistryPath_32Bit = @"SOFTWARE\Microsoft\VisualStudio";
        private const string VisualStudioRegistryPath_64Bit = @"SOFTWARE\Wow6432Node\Microsoft\VisualStudio";

        /// <summary>
        /// Partial path to the code coverage exe, from the Visual Studio shell folder
        /// </summary>
        private const string TeamToolFullPath = @"Team Tools\Dynamic Code Coverage Tools\CodeCoverage.exe";

        private static readonly string[] VsTestToolPlatformInstallerPathToExe =
        {
            @"tools\net451\Team Tools\Dynamic Code Coverage Tools\CodeCoverage.exe", // version https://www.nuget.org/packages/Microsoft.TestPlatform/17.3.2 and below
            @"tools\net462\Team Tools\Dynamic Code Coverage Tools\CodeCoverage.exe"  // version https://www.nuget.org/packages/Microsoft.TestPlatform/17.4.0 and above
        };

        /// <summary>
        /// Code coverage package names for Visual Studio setup configuration
        /// </summary>
        private static readonly string[] CodeCoverageInstallationPackageNames =
        {
            "Microsoft.VisualStudio.TestTools.CodeCoverage",
            "Microsoft.VisualStudio.TestTools.CodeCoverage.Msi"
        };

        private string conversionToolPath;

        #region Public methods

        public BinaryToXmlCoverageReportConverter(ILogger logger, AnalysisConfig config)
            : this(new VisualStudioSetupConfigurationFactory(), logger, config)
        { }

        public BinaryToXmlCoverageReportConverter(IVisualStudioSetupConfigurationFactory setupConfigurationFactory,
            ILogger logger, AnalysisConfig config)
        {
            this.setupConfigurationFactory = setupConfigurationFactory;
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.config = config ?? throw new ArgumentNullException(nameof(config));
        }

        #endregion Public methods

        #region IReportConverter interface

        public bool Initialize()
        {
            bool success;

            conversionToolPath = GetExeToolPath();

            if (conversionToolPath == null)
            {
                logger.LogWarning(Resources.CONV_WARN_FailToFindConversionTool);
                success = false;
            }
            else
            {
                logger.LogDebug(Resources.CONV_DIAG_CommandLineToolInfo, conversionToolPath);
                success = true;
            }
            return success;
        }

        public bool ConvertToXml(string inputFilePath, string outputFilePath)
        {
            if (string.IsNullOrWhiteSpace(inputFilePath))
            {
                throw new ArgumentNullException(nameof(inputFilePath));
            }
            if (string.IsNullOrWhiteSpace(outputFilePath))
            {
                throw new ArgumentNullException(nameof(outputFilePath));
            }

            return ConvertBinaryToXml(conversionToolPath, inputFilePath, outputFilePath, logger);
        }

        #endregion IReportConverter interface

        #region Private methods

        private string GetExeToolPath()
        {
            logger.LogDebug(Resources.CONV_DIAG_LocatingCodeCoverageTool);

            var userSuppliedVsCoverageToolPath = config.GetVsCoverageConverterToolPath();
            if (!string.IsNullOrEmpty(userSuppliedVsCoverageToolPath))
            {
                logger.LogDebug(Resources.CONV_DIAG_LocatingCodeCoverageToolUserSuppliedProperty);
                if (userSuppliedVsCoverageToolPath.EndsWith("CodeCoverage.exe") && File.Exists(userSuppliedVsCoverageToolPath))
                {
                    logger.LogDebug(Resources.CONV_DIAG_CodeCoverageFound, userSuppliedVsCoverageToolPath);
                    return userSuppliedVsCoverageToolPath;
                }

                foreach (var subPath in VsTestToolPlatformInstallerPathToExe)
                {
                    var standardToolInstallerPath = Path.Combine(userSuppliedVsCoverageToolPath, subPath);
                    logger.LogDebug(Resources.CONV_DIAG_CodeCoverageIsNotInVariable, userSuppliedVsCoverageToolPath, subPath);
                    if (File.Exists(standardToolInstallerPath))
                    {
                        logger.LogDebug(Resources.CONV_DIAG_CodeCoverageFound, standardToolInstallerPath);
                        return standardToolInstallerPath;
                    }
                }
                logger.LogWarning(Resources.CONV_WARN_UnableToFindCodeCoverageFileInUserSuppliedVariable);
            }

            return GetExeToolPathFromSetupConfiguration()
                ?? GetExeToolPathFromRegistry();
        }

        #region Code Coverage Tool path from setup configuration

        private string GetExeToolPathFromSetupConfiguration()
        {
            string toolPath = null;

            logger.LogDebug(Resources.CONV_DIAG_LocatingCodeCoverageToolSetupConfiguration);
            var configurationQuery = setupConfigurationFactory.GetSetupConfigurationQuery();
            if (configurationQuery != null)
            {
                var instanceEnumerator = configurationQuery.EnumInstances();

                int fetched;
                var tempInstance = new ISetupInstance[1];

                var instances = new List<ISetupInstance2>();
                //Enumerate the configuration instances
                do
                {
                    instanceEnumerator.Next(1, tempInstance, out fetched);
                    if (fetched > 0)
                    {
                        var instance = (ISetupInstance2)tempInstance[0];
                        if (instance.GetPackages().Any(p => CodeCoverageInstallationPackageNames.Contains(p.GetId())))
                        {
                            //Store instances that have code coverage package installed
                            instances.Add((ISetupInstance2)tempInstance[0]);
                        }
                    }
                } while (fetched > 0);

                if (instances.Count > 1)
                {
                    logger.LogDebug(Resources.CONV_DIAG_MultipleVsVersionsInstalled, string.Join(", ", instances.Select(i =>
                        i.GetInstallationVersion())));
                }

                //Get the installation path for the latest visual studio found
                var visualStudioPath = instances.OrderByDescending(i => i.GetInstallationVersion())
                                                .Select(i => i.GetInstallationPath())
                                                .FirstOrDefault();

                if (visualStudioPath != null)
                {
                    toolPath = Path.Combine(visualStudioPath, TeamToolFullPath);
                }
            }
            else
            {
                logger.LogDebug(Resources.CONV_DIAG_SetupConfigurationNotSupported);
            }

            return toolPath;
        }

        #endregion Code Coverage Tool path from setup configuration

        #region Code Coverage Tool path from registry

        private string GetExeToolPathFromRegistry()
        {
            string toolPath = null;

            logger.LogDebug(Resources.CONV_DIAG_LocatingCodeCoverageToolRegistry);

            var regPath = GetVsRegistryPath(Environment.Is64BitProcess);

            using (var key = Registry.LocalMachine.OpenSubKey(regPath, false))
            {
                // i.e. no VS installed
                if (key == null)
                {
                    return null;
                }

                var keys = key.GetSubKeyNames();

                // Find the ShellFolder paths for the installed VS versions
                var versionFolderMap = GetVsShellFolders(key, keys);

                // Attempt to locate the code coverage tool for each installed version
                var versionToolMap = GetCoverageToolsPaths(versionFolderMap);
                Debug.Assert(!versionToolMap.Keys.Any(k => double.IsNaN(k)), "Version key should be a number");

                if (versionToolMap.Count > 1)
                {
                    logger.LogDebug(Resources.CONV_DIAG_MultipleVsVersionsInstalled, string.Join(", ", versionToolMap.Keys));
                }

                if (versionToolMap.Count > 0)
                {
                    // Use the latest version of the tool
                    var maxVersion = versionToolMap.Keys.Max();
                    toolPath = versionToolMap[maxVersion];
                }
            }

            return toolPath;
        }

        public static string GetVsRegistryPath(bool is64BitProcess)
        {
            // Bug #461: https://github.com/SonarSource/sonar-scanner-msbuild/issues/461
            // The registry path to return depends on whether the OS is 32- or 64-bit, and
            // whether the calling process is 32- or 64-bit.
            // VS is (currently) still a 32-bit process. In most cases we should just return
            // the 32-bit registry path, and rely on registry redirection to re-direct the
            // path to the Wow6432Node if necessary.
            // However, if we are running under a 64-bit process registry redirection won't
            // happen, so we need to supply the full path ourselves.
            return is64BitProcess ? VisualStudioRegistryPath_64Bit : VisualStudioRegistryPath_32Bit;
        }

        /// <summary>
        /// Returns a mapping of VS version (as a string e.g. "12.0") to the install directory for that version
        /// </summary>
        private static IDictionary<string, string> GetVsShellFolders(RegistryKey vsKey, string[] keys)
        {
            var versionFolderMap = new Dictionary<string, string>();
            foreach (var key in keys)
            {
                // Check for the shell dir subkey
                if (Regex.IsMatch(key, @"\d+.\d+", RegexOptions.None, RegexConstants.DefaultTimeout) && Registry.GetValue(vsKey.Name + "\\" + key, "ShellFolder", null)
                    is string shellFolder)
                {
                    versionFolderMap[key] = shellFolder;
                }
            }
            return versionFolderMap;
        }

        /// <summary>
        /// Returns a mapping of VS version (as a double) to the full path to the code coverage
        /// tool for that version.
        /// </summary>
        /// <remarks>VS versions that cannot be converted successfully to a double will be ignored.
        /// The returned map will only have entries for VS version for which the code coverage tool could be found.</remarks>
        private static IDictionary<double, string> GetCoverageToolsPaths(IDictionary<string, string> versionFolderMap)
        {
            var versionPathMap = new Dictionary<double, string>();
            foreach (var kvp in versionFolderMap)
            {
                var toolPath = Path.Combine(kvp.Value, TeamToolFullPath);
                if (File.Exists(toolPath))
                {
                    var version = TryGetVersionAsDouble(kvp.Key);

                    if (!double.IsNaN(version))
                    {
                        versionPathMap[version] = toolPath;
                    }
                }
            }
            return versionPathMap;
        }

        /// <summary>
        /// Attempts to convert the supplied version to a double.
        /// Returns NaN if the value could not be converted
        /// </summary>
        private static double TryGetVersionAsDouble(string versionKey)
        {
            if (!double.TryParse(versionKey, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out double result))
            {
                result = double.NaN;
            }
            return result;
        }

        #endregion Code Coverage Tool path from registry

        // was internal
        public static bool ConvertBinaryToXml(string converterExeFilePath, string inputBinaryFilePath, string outputXmlFilePath,
            ILogger logger)
        {
            Debug.Assert(!string.IsNullOrEmpty(converterExeFilePath), "Expecting the conversion tool path to have been set");
            Debug.Assert(File.Exists(converterExeFilePath), "Expecting the converter exe to exist: " + converterExeFilePath);
            Debug.Assert(Path.IsPathRooted(inputBinaryFilePath), "Expecting the input file name to be a full absolute path");
            Debug.Assert(File.Exists(inputBinaryFilePath), "Expecting the input file to exist: " + inputBinaryFilePath);
            Debug.Assert(Path.IsPathRooted(outputXmlFilePath), "Expecting the output file name to be a full absolute path");

            var args = new List<string>
            {
                "analyze",
                string.Format(CultureInfo.InvariantCulture, @"/output:{0}", outputXmlFilePath),
                inputBinaryFilePath
            };

            var scannerArgs = new ProcessRunnerArguments(converterExeFilePath, false)
            {
                WorkingDirectory = Path.GetDirectoryName(outputXmlFilePath),
                CmdLineArgs = args,
                TimeoutInMilliseconds = ConversionTimeoutInMs
            };

            var runner = new ProcessRunner(logger);
            var success = runner.Execute(scannerArgs);

            if (success)
            {
                // Check the output file actually exists
                if (!File.Exists(outputXmlFilePath))
                {
                    logger.LogError(Resources.CONV_ERROR_OutputFileNotFound, outputXmlFilePath);
                    success = false;
                }
            }
            else
            {
                logger.LogError(Resources.CONV_ERROR_ConversionToolFailed, inputBinaryFilePath);
            }

            return success;
        }

        #endregion Private methods
    }
}
