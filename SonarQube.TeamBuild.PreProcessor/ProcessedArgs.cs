//-----------------------------------------------------------------------
// <copyright file="ProcessedArgs.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using SonarQube.Common;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SonarQube.TeamBuild.PreProcessor
{
    /// <summary>
    /// Data class to hold validated command line arguments required by the pre-processor
    /// </summary>
    public class ProcessedArgs
    {
        private readonly ListPropertiesProvider projectSettingsProvider;
        private readonly IAnalysisPropertyProvider cmdLineProperties;
        private readonly IAnalysisPropertyProvider globalFileProperties;

        private readonly IAnalysisPropertyProvider aggProperties;

        public ProcessedArgs(string key, string name, string version, IAnalysisPropertyProvider cmdLineProperties, IAnalysisPropertyProvider globalFileProperties)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentNullException("key");
            }
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentNullException("name");
            }
            if (string.IsNullOrWhiteSpace(version))
            {
                throw new ArgumentNullException("version");
            }
            if (cmdLineProperties == null)
            {
                throw new ArgumentNullException("cmdLineProperties");
            }
            if (globalFileProperties == null)
            {
                throw new ArgumentNullException("globalFileProperties");
            }

            this.cmdLineProperties = cmdLineProperties;
            this.globalFileProperties = globalFileProperties;

            this.projectSettingsProvider = new ListPropertiesProvider();
            this.projectSettingsProvider.AddProperty(SonarProperties.ProjectKey, key);
            this.projectSettingsProvider.AddProperty(SonarProperties.ProjectName, name);
            this.projectSettingsProvider.AddProperty(SonarProperties.ProjectVersion, version);

            this.aggProperties = new AggregatePropertiesProvider(projectSettingsProvider, cmdLineProperties, globalFileProperties);
        }

        public string ProjectKey { get { return this.GetSetting(SonarProperties.ProjectKey); } }

        public string ProjectName { get { return this.GetSetting(SonarProperties.ProjectName); } }

        public string ProjectVersion { get { return this.GetSetting(SonarProperties.ProjectVersion); } }

        public IAnalysisPropertyProvider ProjectSettingsProvider { get { return this.projectSettingsProvider; } }

        public IAnalysisPropertyProvider CmdLineProperties {  get { return this.cmdLineProperties; } }

        public IAnalysisPropertyProvider GlobalFileProperties { get { return this.globalFileProperties; } }

        public string GetSetting(string key)
        {
            string value;
            this.aggProperties.TryGetValue(key, out value);
            return value;
        }

        public string GetSetting(string key, string defaultValue)
        {
            string value;
            if (!this.aggProperties.TryGetValue(key, out value))
            {
                value = defaultValue;
            }
            return value;
        }

        public IEnumerable<Property> GetAllProperties()
        {
            return this.aggProperties.GetAllProperties();
        }
    }
}
