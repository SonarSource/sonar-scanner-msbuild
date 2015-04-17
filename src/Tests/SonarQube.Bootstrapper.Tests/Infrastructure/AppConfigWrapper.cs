//-----------------------------------------------------------------------
// <copyright file="AppConfigWrapper.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using System;

namespace SonarQube.Bootstrapper.Tests
{
    /// <summary>
    /// Wrapper to allow setting and clearing our application config settings that are
    /// managed by the .Net framework.
    /// Based on the info in this posting (thanks Martin!):
    /// http://stackoverflow.com/questions/3719107/modifying-application-settings-in-unit-tests
    /// </summary>
    internal sealed class AppConfigWrapper
    {
        private readonly Properties.Settings appConfig;

        #region Public methods

        public AppConfigWrapper()
        {
            this.appConfig = new Properties.Settings();
        }

        public Properties.Settings AppConfig { get { return this.appConfig; } }

        public void Reset()
        {
            this.appConfig.Reset();
        }

        public void SetDownloadDir(string value)
        {
            // Dummy access to ensure the properties are initialised
            string dummy = this.appConfig.DownloadDir;
            this.appConfig.PropertyValues["DownloadDir"].PropertyValue = value;
        }

        public void SetPreProcessExe(string value)
        {
            // Dummy access to ensure the properties are initialised
            string dummy = this.appConfig.PreProcessExe;
            this.appConfig.PropertyValues["PreProcessExe"].PropertyValue = value;
        }

        public void SetPostProcessExe(string value)
        {
            // Dummy access to ensure the properties are initialised
            string dummy = this.appConfig.PostProcessExe;
            this.appConfig.PropertyValues["PostProcessExe"].PropertyValue = value;
        }

        public void SetSonarQubeUrl(string value)
        {
            // Dummy access to ensure the properties are initialised
            string dummy = this.appConfig.SonarQubeUrl;
            this.appConfig.PropertyValues["SonarQubeUrl"].PropertyValue = value;
        }

        #endregion
    }
}
