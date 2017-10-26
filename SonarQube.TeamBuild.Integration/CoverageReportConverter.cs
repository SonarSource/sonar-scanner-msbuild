/*
 * SonarQube Scanner for MSBuild
 * Copyright (C) 2016-2017 SonarSource SA
 * mailto:info AT sonarsource DOT com
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

using Microsoft.Win32;
using SonarQube.Common;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.Setup.Configuration;

namespace SonarQube.TeamBuild.Integration
{
    public class CoverageReportConverter : ICoverageReportConverter
    {
        private const int ConversionTimeoutInMs = 60000;
        private readonly IVisualStudioSetupConfigurationFactory setupConfigurationFactory;

        /// <summary>
        /// Registry containing information about installed VS versions
        /// </summary>
        private const string VisualStudioRegistryPath = @"SOFTWARE\Microsoft\VisualStudio";

        /// <summary>
        /// Partial path to the code coverage exe, from the Visual Studio shell folder
        /// </summary>
        private const string TeamToolPathandExeName = @"Team Tools\Dynamic Code Coverage Tools\CodeCoverage.exe";

        /// <summary>
        /// Code coverage package name for Visual Studio setup configuration
        /// </summary>
        private const string CodeCoverageInstallationPackage = "Microsoft.VisualStudio.TestTools.CodeCoverage";

        private string conversionToolPath;

        #region Public methods

        public CoverageReportConverter()
            : this(new VisualStudioSetupConfigurationFactory())
        { }

        public CoverageReportConverter(IVisualStudioSetupConfigurationFactory setupConfigurationFactory)
        {
            this.setupConfigurationFactory = setupConfigurationFactory;
        }

        #endregion Public methods

        #region IReportConverter interface

        public bool Initialize(ILogger logger)
        {
            if (logger == null)
            {
                throw new ArgumentNullException("logger");
            }

            bool success;

            this.conversionToolPath = GetExeToolPath(logger);

            if (this.conversionToolPath == null)
            {
                logger.LogWarning(Resources.CONV_WARN_FailToFindConversionTool);
                success = false;
            }
            else
            {
                logger.LogDebug(Resources.CONV_DIAG_CommandLineToolInfo, this.conversionToolPath);
                success = true;
            }
            return success;
        }

        public bool ConvertToXml(string inputFilePath, string outputFilePath, ILogger logger)
        {
            if (string.IsNullOrWhiteSpace(inputFilePath))
            {
                throw new ArgumentNullException("inputFilePath");
            }
            if (string.IsNullOrWhiteSpace(outputFilePath))
            {
                throw new ArgumentNullException("outputFilePath");
            }
            if (logger == null)
            {
                throw new ArgumentNullException("logger");
            }

            return ConvertBinaryToXml(this.conversionToolPath, inputFilePath, outputFilePath, logger);
        }

        #endregion IReportConverter interface

        #region Private methods

        private string GetExeToolPath(ILogger logger)
        {
            logger.LogDebug(Resources.CONV_DIAG_LocatingCodeCoverageTool);
            return GetExeToolPathFromSetupConfiguration(logger) ??
                   GetExeToolPathFromRegistry(logger);
        }

        #region Code Coverage Tool path from setup configuration

        private string GetExeToolPathFromSetupConfiguration(ILogger logger)
        {
            string toolPath = null;

            logger.LogDebug(Resources.CONV_DIAG_LocatingCodeCoverageToolSetupConfiguration);
            ISetupConfiguration configurationQuery = setupConfigurationFactory.GetSetupConfigurationQuery();
            if (configurationQuery != null)
            {
                IEnumSetupInstances instanceEnumerator = configurationQuery.EnumInstances();

                int fetched;
                ISetupInstance[] tempInstance = new ISetupInstance[1];

                List<ISetupInstance2> instances = new List<ISetupInstance2>();
                //Enumerate the configuration instances
                do
                {
                    instanceEnumerator.Next(1, tempInstance, out fetched);
                    if (fetched > 0)
                    {
                        ISetupInstance2 instance = (ISetupInstance2)tempInstance[0];
                        if (instance.GetPackages().Any(p => p.GetId() == CodeCoverageInstallationPackage))
                        {
                            //Store instances that have code coverage package installed
                            instances.Add((ISetupInstance2)tempInstance[0]);
                        }
                    }
                } while (fetched > 0);

                if (instances.Count > 1)
                {
                    logger.LogDebug(Resources.CONV_DIAG_MultipleVsVersionsInstalled, string.Join(", ", instances.Select(i => i.GetInstallationVersion())));
                }

                //Get the installation path for the latest visual studio found
                var visualStudioPath = instances.OrderByDescending(i => i.GetInstallationVersion())
                                                .Select(i => i.GetInstallationPath())
                                                .FirstOrDefault();

                if (visualStudioPath != null)
                {
                    toolPath = Path.Combine(visualStudioPath, TeamToolPathandExeName);
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

        private static string GetExeToolPathFromRegistry(ILogger logger)
        {
            string toolPath = null;

            logger.LogDebug(Resources.CONV_DIAG_LocatingCodeCoverageToolRegistry);
            using (RegistryKey key = Registry.LocalMachine.OpenSubKey(VisualStudioRegistryPath, false))
            {
                // i.e. no VS installed
                if (key == null)
                {
                    return null;
                }

                string[] keys = key.GetSubKeyNames();

                // Find the ShellFolder paths for the installed VS versions
                IDictionary<string, string> versionFolderMap = GetVsShellFolders(key, keys);

                // Attempt to locate the code coverage tool for each installed version
                IDictionary<double, string> versionToolMap = GetCoverageToolsPaths(versionFolderMap);
                Debug.Assert(!versionToolMap.Keys.Any(k => double.IsNaN(k)), "Version key should be a number");

                if (versionToolMap.Count > 1)
                {
                    logger.LogDebug(Resources.CONV_DIAG_MultipleVsVersionsInstalled, string.Join(", ", versionToolMap.Keys));
                }

                if (versionToolMap.Count > 0)
                {
                    // Use the latest version of the tool
                    double maxVersion = versionToolMap.Keys.Max();
                    toolPath = versionToolMap[maxVersion];
                }
            }

            return toolPath;
        }

        /// <summary>
        /// Returns a mapping of VS version (as a string e.g. "12.0") to the install directory for that version
        /// </summary>
        private static IDictionary<string, string> GetVsShellFolders(RegistryKey vsKey, string[] keys)
        {
            Dictionary<string, string> versionFolderMap = new Dictionary<string, string>();
            foreach (string key in keys)
            {
                if (Regex.IsMatch(key, @"\d+.\d+"))
                {
                    // Check for the shell dir subkey
                    string shellFolder = Registry.GetValue(vsKey.Name + "\\" + key, "ShellFolder", null) as string;
                    if (shellFolder != null)
                    {
                        versionFolderMap[key] = shellFolder;
                    }
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
            Dictionary<double, string> versionPathMap = new Dictionary<double, string>();
            foreach (KeyValuePair<string, string> kvp in versionFolderMap)
            {
                string toolPath = Path.Combine(kvp.Value, TeamToolPathandExeName);
                if (File.Exists(toolPath))
                {
                    double version = TryGetVersionAsDouble(kvp.Key);

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
            double result;
            if (!double.TryParse(versionKey, System.Globalization.NumberStyles.AllowDecimalPoint, System.Globalization.CultureInfo.InvariantCulture, out result))
            {
                result = double.NaN;
            }
            return result;
        }

        #endregion Code Coverage Tool path from registry

        // was internal
        public static bool ConvertBinaryToXml(string converterExeFilePath, string inputBinaryFilePath, string outputXmlFilePath, ILogger logger)
        {
            Debug.Assert(!string.IsNullOrEmpty(converterExeFilePath), "Expecting the conversion tool path to have been set");
            Debug.Assert(File.Exists(converterExeFilePath), "Expecting the converter exe to exist: " + converterExeFilePath);
            Debug.Assert(Path.IsPathRooted(inputBinaryFilePath), "Expecting the input file name to be a full absolute path");
            Debug.Assert(File.Exists(inputBinaryFilePath), "Expecting the input file to exist: " + inputBinaryFilePath);
            Debug.Assert(Path.IsPathRooted(outputXmlFilePath), "Expecting the output file name to be a full absolute path");

            List<string> args = new List<string>();
            args.Add("analyze");
            args.Add(string.Format(System.Globalization.CultureInfo.InvariantCulture, @"/output:{0}", outputXmlFilePath));
            args.Add(inputBinaryFilePath);

            ProcessRunnerArguments scannerArgs = new ProcessRunnerArguments(converterExeFilePath, false, logger)
            {
                WorkingDirectory = Path.GetDirectoryName(outputXmlFilePath),
                CmdLineArgs = args,
                TimeoutInMilliseconds = ConversionTimeoutInMs
            };

            ProcessRunner runner = new ProcessRunner();
            bool success = runner.Execute(scannerArgs);

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