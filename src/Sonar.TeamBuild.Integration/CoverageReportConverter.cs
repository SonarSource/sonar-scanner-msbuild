//-----------------------------------------------------------------------
// <copyright file="CoverageReportConverter.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.Win32;
using Sonar.Common;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Sonar.TeamBuild.Integration
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

        #region Public methods

        #endregion

        #region IReportConverter interface

        public bool Initialize(ILogger logger)
        {
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

        public bool ConvertToXml(string inputFullBinaryFileName, string outputFullXmlFileName, ILogger logger)
        {
            if (string.IsNullOrWhiteSpace(inputFullBinaryFileName))
            {
                throw new ArgumentNullException("inputFullBinaryFileName");
            }
            if (string.IsNullOrWhiteSpace(outputFullXmlFileName))
            {
                throw new ArgumentNullException("outputFullXmlFileName");
            }
            if (logger == null)
            {
                throw new ArgumentNullException("logger");
            }
            
            return ConvertBinaryToXml(this.conversionToolPath, inputFullBinaryFileName, outputFullXmlFileName, logger);
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
                IDictionary<string, string> versionToolMap = GetCoverageToolsPaths(versionFolderMap);

                string maxVersion = versionFolderMap.Keys.Max(k => double.Parse(k, System.Globalization.NumberStyles.AllowDecimalPoint)).ToString();

                if (versionToolMap.Count > 1)
                {
                    logger.LogMessage(Resources.CONV_DIAG_MultipleVsVersionsInstalled, string.Join(", ", versionToolMap.Keys));
                }

                if (versionToolMap.Count > 0)
                {
                    toolPath = versionToolMap.Last().Value;
                }
            }   

            return toolPath;
        }

        private static IDictionary<string, string> GetVsShellFolders(RegistryKey vsKey, string[] keys)
        {
            Dictionary<string, string> versionFolderMap = new Dictionary<string, string>();
            foreach(string key in keys)
            {
                if (Regex.IsMatch(key, @"\d+.\d+"))
                {
                    // Check for the shell dir subkey
                    string shellDir = vsKey.GetValue(key + "\\ShellFolder", null) as string;

                    string shellFolder = Registry.GetValue(vsKey.Name + "\\" + key, "ShellFolder", null) as string;
                    if (shellFolder != null)
                    {
                        versionFolderMap[key] = shellFolder;
                    }
                }
            }
            return versionFolderMap;
        }

        private static IDictionary<string, string> GetCoverageToolsPaths(IDictionary<string, string> versionFolderMap)
        {
            Dictionary<string, string> versionPathMap = new Dictionary<string, string>();
            foreach (KeyValuePair<string, string> kvp in versionFolderMap)
            {
                string toolPath = Path.Combine(kvp.Value, TeamToolPathandExeName);
                if (File.Exists(toolPath))
                {
                    versionPathMap[kvp.Key] = toolPath;
                }
            }
            return versionPathMap;
        }

        internal static bool ConvertBinaryToXml(string converterExeFilePath, string inputfullBinaryFilePath, string outputFullXmlFilePath, ILogger logger)
        {
            Debug.Assert(!string.IsNullOrEmpty(converterExeFilePath), "Expecting the conversion tool path to have been set");
            Debug.Assert(File.Exists(converterExeFilePath), "Expecting the converter exe to exist: " + converterExeFilePath);
            Debug.Assert(Path.IsPathRooted(inputfullBinaryFilePath), "Expecting the input file name to be a full absolute path");
            Debug.Assert(File.Exists(inputfullBinaryFilePath), "Expecting the input file to exist: " + inputfullBinaryFilePath);
            Debug.Assert(Path.IsPathRooted(outputFullXmlFilePath), "Expecting the output file name to be a full absolute path");

            string args = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                @"analyze /output:""{0}"" ""{1}""",
                outputFullXmlFilePath, inputfullBinaryFilePath);

            ProcessRunner runner = new ProcessRunner();
            bool success = runner.Execute(converterExeFilePath, args, Path.GetDirectoryName(outputFullXmlFilePath), ConversionTimeoutInMs, logger);

            // Check the output file actually exists
            if (success)
            {
                if (!File.Exists(outputFullXmlFilePath))
                {
                    logger.LogError(Resources.CONV_ERROR_OutputFileNotFound, outputFullXmlFilePath);
                    success = false;
                }
            }

            return success;
        }

        #endregion

    }
}
