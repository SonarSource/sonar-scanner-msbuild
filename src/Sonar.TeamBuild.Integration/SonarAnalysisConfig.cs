//-----------------------------------------------------------------------
// <copyright file="SonarAnalysisConfig.cs" company="SonarSource SA and Microsoft Corporation">
//   (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

using Sonar.Common;
using System.Xml.Serialization;

namespace Sonar.TeamBuild.Integration
{
    /// <summary>
    /// Data class to describe the analysis settings for a single Sonar project
    /// </summary>
    /// <remarks>The class is XML-serializable</remarks>
    [XmlRoot(Namespace = XmlNamespace)]
    public class SonarAnalysisConfig
    {
        public const string XmlNamespace = Sonar.Common.ProjectInfo.XmlNamespace;

        public string TfsUri { get; set; }

        public string BuildUri { get; set; }

        public string SonarConfigDir { get; set; }

        public string SonarOutputDir { get; set; }

        #region Sonar project properties

        public string SonarProjectKey { get; set; }

        public string SonarProjectVersion { get; set; }

        public string SonarProjectName { get; set; }

        public string SonarRunnerPropertiesPath { get; set; }

        #endregion
    }
}
