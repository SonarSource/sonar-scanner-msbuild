//-----------------------------------------------------------------------
// <copyright file="CoverageReportConverter.cs" company="SonarSource SA and Microsoft Corporation">
//   (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
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
            
            return this.Execute(inputFullBinaryFileName, outputFullXmlFileName, logger);
        }

        #endregion

        #region Private methods

        private static string GetExeToolPath(ILogger logger)
        {
            string toolPath = null;

            logger.LogMessage("Attempting to locate the CodeCoverage.exe tool...");
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
                    logger.LogMessage("Multiple versions of VS are installed: {0}", string.Join(", ", versionToolMap.Keys));
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

        private bool Execute(string inputfullBinaryFileName, string outputFullXmlFileName, ILogger logger)
        {
            Debug.Assert(!string.IsNullOrEmpty(this.conversionToolPath), "Expecting the conversion tool path to have been set");

            string args = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                @"analyze /output:""{0}"" {1}",
                outputFullXmlFileName, inputfullBinaryFileName);

            // TODO: capture errors from the remote process
            ProcessStartInfo psi = new ProcessStartInfo(this.conversionToolPath, args);
            psi.CreateNoWindow = true;
            
            Process process = Process.Start(psi);
            bool success = process.WaitForExit(ConversionTimeoutMs);
            return success;
        }

        #endregion

    }
}
