/*
 * SonarQube Scanner for MSBuild
 * Copyright (C) 2015-2017 SonarSource SA and Microsoft Corporation
 * mailto: contact AT sonarsource DOT com
 *
 * Licensed under the MIT License.
 * See LICENSE file in the project root for full license information.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
 * THE SOFTWARE.
 */

using SonarQube.Common;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace SonarQube.TeamBuild.PreProcessor
{
    /// <summary>
    /// Data class to hold validated command line arguments required by the pre-processor
    /// </summary>
    public class ProcessedArgs
    {
        private readonly string sonarQubeUrl;
        private readonly string projectKey;
        private readonly string projectName;
        private readonly string projectVersion;

        private readonly IAnalysisPropertyProvider cmdLineProperties;
        private readonly IAnalysisPropertyProvider globalFileProperties;
        private readonly IAnalysisPropertyProvider scannerEnvProperties;
        private readonly IAnalysisPropertyProvider aggProperties;

        public ProcessedArgs(string key, string name, string version, bool installLoaderTargets, IAnalysisPropertyProvider cmdLineProperties,
            IAnalysisPropertyProvider globalFileProperties, IAnalysisPropertyProvider scannerEnvProperties)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentNullException("key");
            }
            if (cmdLineProperties == null)
            {
                throw new ArgumentNullException("cmdLineProperties");
            }
            if (globalFileProperties == null)
            {
                throw new ArgumentNullException("globalFileProperties");
            }
            if (scannerEnvProperties == null)
            {
                throw new ArgumentNullException("scannerEnvProperties");
            }

            this.projectKey = key;
            this.projectName = name;
            this.projectVersion = version;

            this.cmdLineProperties = cmdLineProperties;
            this.globalFileProperties = globalFileProperties;
            this.scannerEnvProperties = scannerEnvProperties;
            this.InstallLoaderTargets = installLoaderTargets;

            this.aggProperties = new AggregatePropertiesProvider(cmdLineProperties, globalFileProperties, scannerEnvProperties);
            if (!aggProperties.TryGetValue(SonarProperties.HostUrl, out this.sonarQubeUrl))
            {
                this.sonarQubeUrl = "http://localhost:9000";
            }
        }

        public string ProjectKey { get { return this.projectKey; } }

        public string ProjectName { get { return this.projectName; } }

        public string ProjectVersion { get { return this.projectVersion; } }

        public string SonarQubeUrl { get { return this.sonarQubeUrl; } }

        /// <summary>
        /// If true the preprocessor should copy the loader targets to a user location where MSBuild will pick them up
        /// </summary>
        public bool InstallLoaderTargets { get; private set; }

        /// <summary>
        /// Returns the combined command line and file analysis settings
        /// </summary>
        public IAnalysisPropertyProvider AggregateProperties { get { return this.aggProperties; } }

        public IAnalysisPropertyProvider CmdLineProperties { get { return this.cmdLineProperties; } }

        public IAnalysisPropertyProvider ScannerEnvProperties { get { return this.scannerEnvProperties; } }

        /// <summary>
        /// Returns the name of property settings file or null if there is not one
        /// </summary>
        public string PropertiesFileName
        {
            get
            {
                FilePropertyProvider fileProvider = this.globalFileProperties as FilePropertyProvider;
                if (fileProvider != null)
                {
                    Debug.Assert(fileProvider.PropertiesFile != null, "File properties should not be null");
                    Debug.Assert(!string.IsNullOrWhiteSpace(fileProvider.PropertiesFile.FilePath), "Settings file name should not be null");
                    return fileProvider.PropertiesFile.FilePath;
                }
                return null;
            }
        }

        /// <summary>
        /// Returns the value for the specified setting.
        /// Throws if the setting does not exist.
        /// </summary>
        public string GetSetting(string key)
        {
            string value;
            if (!this.aggProperties.TryGetValue(key, out value))
            {
                string message = string.Format(System.Globalization.CultureInfo.CurrentCulture, Resources.ERROR_MissingSetting, key);
                throw new InvalidOperationException(message);
            }
            return value;
        }

        /// <summary>
        /// Returns the value for the specified setting, or the supplied
        /// default if the setting does not exist
        /// </summary>
        public string GetSetting(string key, string defaultValue)
        {
            string value;
            if (!this.aggProperties.TryGetValue(key, out value))
            {
                value = defaultValue;
            }
            return value;
        }

        public bool TryGetSetting(string key, out string value)
        {
            return this.aggProperties.TryGetValue(key, out value);
        }

        public IEnumerable<Property> GetAllProperties()
        {
            return this.aggProperties.GetAllProperties();
        }
    }
}
