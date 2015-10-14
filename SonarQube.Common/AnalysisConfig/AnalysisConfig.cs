//-----------------------------------------------------------------------
// <copyright file="AnalysisConfig.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All Rights Reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace SonarQube.Common
{
    /// <summary>
    /// Data class to describe the analysis settings for a single SonarQube project
    /// </summary>
    /// <remarks>The class is XML-serializable</remarks>
    [XmlRoot(Namespace = XmlNamespace)]
    public class AnalysisConfig
    {
        public const string XmlNamespace = ProjectInfo.XmlNamespace;

        public string SonarConfigDir { get; set; }

        public string SonarOutputDir { get; set; }

        public string SonarBinDir { get; set; }

        /// <summary>
        /// The working directory as perceived by the user, i.e. the directory containing the solution
        /// </summary>
        /// <remarks>Users expect to specify paths relative to the build directory and not to the location of the sonar-runner program.
       ///  See https://jira.sonarsource.com/browse/SONARMSBRU-100 for details.</remarks>
        public string SonarRunnerWorkingDirectory { get; set; }

        #region SonarQube project properties

        public string SonarQubeHostUrl { get; set; }

        public string SonarProjectKey { get; set; }

        public string SonarProjectVersion { get; set; }

        public string SonarProjectName { get; set; }

        /// <summary>
        /// List of additional configuration-related settings
        /// e.g. the build system identifier, if appropriate.
        /// </summary>
        /// <remarks>These settings will not be supplied to the sonar-runner.</remarks>
        public List<ConfigSetting> AdditionalConfig { get; set; }

        /// <summary>
        /// List of analysis settings inherited from the SonarQube server
        /// </summary>
        public AnalysisProperties ServerSettings{ get; set; }

        /// <summary>
        /// List of analysis settings supplied locally (either on the
        /// command line or in a file)
        /// </summary>
        public AnalysisProperties LocalSettings { get; set; }

        #endregion

        #region Serialization

        [XmlIgnore]
        public string FileName { get; private set; }

        /// <summary>
        /// Saves the project to the specified file as XML
        /// </summary>
        public void Save(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentNullException("fileName");
            }

            Serializer.SaveModel(this, fileName);
            this.FileName = fileName;
        }

        /// <summary>
        /// Loads and returns project info from the specified XML file
        /// </summary>
        public static AnalysisConfig Load(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentNullException("fileName");
            }

            AnalysisConfig model = Serializer.LoadModel<AnalysisConfig>(fileName);
            model.FileName = fileName;
            return model;
        }

        #endregion

    }
}
