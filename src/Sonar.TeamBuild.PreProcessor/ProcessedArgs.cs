//-----------------------------------------------------------------------
// <copyright file="ProcessedArgs.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

namespace SonarQube.TeamBuild.PreProcessor
{
    /// <summary>
    /// Data class to hold validated command line arguments
    /// </summary>
    public class ProcessedArgs
    {
        private readonly string key;
        private readonly string version;
        private readonly string name;
        private readonly string propertiesPath;

        public ProcessedArgs(string key, string name, string version, string propertiesPath)
        {
            this.key = key;
            this.name = name;
            this.version = version;
            this.propertiesPath = propertiesPath;
        }

        public string ProjectKey { get { return this.key; } }

        public string ProjectName { get { return this.name; } }

        public string ProjectVersion { get { return this.version; } }

        public string RunnerPropertiesPath { get { return this.propertiesPath; } }
    }
}
