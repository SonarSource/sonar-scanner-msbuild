//-----------------------------------------------------------------------
// <copyright file="CoverageReportConverter.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.Win32;
using SonarQube.Common;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace SonarQube.TeamBuild.Integration
{
    public class CoverageReportConverter : ICoverageReportConverter
    {
        private const int ConversionTimeoutInMs = 30000;

        /// <summary>
        /// Registry containing information about installed VS versions
        /// </summary>
        private const string VisualStudioRegistryPath = @"SOFTWARE\Microsoft\VisualStudio";

        /// <summary>
        /// Partial path to the code coverage exe, from the Visual Studio shell folder
        /// </summary>
        private const string TeamToolPathandExeName = @"Team Tools\Dynamic Code Coverage Tools\CodeCoverage.exe";
        
        private const int ConversionTimeoutMs = 5000;

        private string conversionToolPath;

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
                logger.LogError(Resources.CONV_ERROR_FailToFindConversionTool);
                success = false;
            }
            else
            {
                Debug.Assert(File.Exists(this.conversionToolPath), "Expecting the code coverage exe to exist. Full name: " + this.conversionToolPath);
                logger.LogMessage(Resources.CONV_DIAG_CommandLineToolInfo, this.conversionToolPath);
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

        #endregion

        #region Private methods

        private static string GetExeToolPath(ILogger logger)
        {
            string toolPath = null;

            logger.LogMessage(Resources.CONV_DIAG_LocatingCodeCoverageTool);
            using (RegistryKey key = Registry.LocalMachine.OpenSubKey(VisualStudioRegistryPath, false))
            {
                string[] keys = key.GetSubKeyNames();

                // Find the ShellFolder paths for the installed VS versions
                IDictionary<string, string> versionFolderMap = GetVsShellFolders(key, keys);

                // Attempt to locate the code coverage tool for each installed version
                IDictionary<double, string> versionToolMap = GetCoverageToolsPaths(versionFolderMap);
                Debug.Assert(!versionToolMap.Keys.Any(k => double.IsNaN(k)), "Version key should be a number");
                
                if (versionToolMap.Count > 1)
                {
                    logger.LogMessage(Resources.CONV_DIAG_MultipleVsVersionsInstalled, string.Join(", ", versionToolMap.Keys));
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
            foreach(string key in keys)
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


        // was internal
        public static bool ConvertBinaryToXml(string converterExeFilePath, string inputBinaryFilePath, string outputXmlFilePath, ILogger logger)
        {
            Debug.Assert(!string.IsNullOrEmpty(converterExeFilePath), "Expecting the conversion tool path to have been set");
            Debug.Assert(File.Exists(converterExeFilePath), "Expecting the converter exe to exist: " + converterExeFilePath);
            Debug.Assert(Path.IsPathRooted(inputBinaryFilePath), "Expecting the input file name to be a full absolute path");
            Debug.Assert(File.Exists(inputBinaryFilePath), "Expecting the input file to exist: " + inputBinaryFilePath);
            Debug.Assert(Path.IsPathRooted(outputXmlFilePath), "Expecting the output file name to be a full absolute path");

            string args = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                @"analyze /output:""{0}"" ""{1}""",
                outputXmlFilePath, inputBinaryFilePath);

            ProcessRunner runner = new ProcessRunner();
            bool success = runner.Execute(converterExeFilePath, args, Path.GetDirectoryName(outputXmlFilePath), ConversionTimeoutInMs, logger);

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

        #endregion

    }
}
