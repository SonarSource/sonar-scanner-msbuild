//-----------------------------------------------------------------------
// <copyright file="AnalysisConfig.cs" company="SonarSource SA and Microsoft Corporation">
//   (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace Sonar.Common
{
    /// <summary>
    /// Data class to describe the analysis settings for a single Sonar project
    /// </summary>
    /// <remarks>The class is XML-serializable</remarks>
    [XmlRoot(Namespace = XmlNamespace)]
    public class AnalysisConfig
    {
        public const string XmlNamespace = Sonar.Common.ProjectInfo.XmlNamespace;

        public string SonarConfigDir { get; set; }

        public string SonarOutputDir { get; set; }

        #region Sonar project properties

        public string SonarProjectKey { get; set; }

        public string SonarProjectVersion { get; set; }

        public string SonarProjectName { get; set; }

        public string SonarRunnerPropertiesPath { get; set; }

        /// <summary>
        /// List of additional analysis settings
        /// </summary>
        public List<AnalysisSetting> AdditionalSettings { get; set; }

        #endregion

        #region Serialization

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
            return model;
        }

        #endregion

    }
}
