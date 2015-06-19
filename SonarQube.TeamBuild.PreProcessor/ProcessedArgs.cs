//-----------------------------------------------------------------------
// <copyright file="ProcessedArgs.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using SonarQube.Common;
using System.Collections.Generic;
using System.Linq;

namespace SonarQube.TeamBuild.PreProcessor
{
    /// <summary>
    /// Data class to hold validated command line arguments
    /// </summary>
    public class ProcessedArgs
    {
        private readonly string projectKey;
        private readonly string version;
        private readonly string name;
        private readonly string propertiesPath;
        private readonly IEnumerable<AnalysisSetting> additionalSettings;

        public ProcessedArgs(string key, string name, string version, string propertiesPath, IEnumerable<AnalysisSetting> additionalSettings)
        {
            this.projectKey = key;
            this.name = name;
            this.version = version;
            this.propertiesPath = propertiesPath;
            this.additionalSettings = new List<AnalysisSetting>(additionalSettings);
        }

        public string ProjectKey { get { return this.projectKey; } }

        public string ProjectName { get { return this.name; } }

        public string ProjectVersion { get { return this.version; } }

        public string RunnerPropertiesPath { get { return this.propertiesPath; } }

        public IDictionary<string, string> AdditionalSettings { get; set; }

        public string GetSetting(string key)
        {
            AnalysisSetting setting = this.additionalSettings.FirstOrDefault(s => AnalysisSetting.SettingKeyComparer.Equals(key, s.Id));
            return setting == null ? null : setting.Value;
        }
    }
}
