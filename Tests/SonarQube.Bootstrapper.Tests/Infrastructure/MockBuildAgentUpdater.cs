//-----------------------------------------------------------------------
// <copyright file="MockBuildAgentUpdater.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using System;
using SonarQube.Common;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SonarQube.Bootstrapper.Tests
{
    internal class MockBuildAgentUpdater : IBuildAgentUpdater
    {
        private bool tryUpdateCalled;
        private bool checkVersionCalled;
        private bool injectTargetsCalled;

        public class UpdatingEventArgs : EventArgs
        {
            private readonly string hostUrl;
            private readonly string targetDir;

            public UpdatingEventArgs(string hostUrl, string targetDir)
            {
                this.hostUrl = hostUrl;
                this.targetDir = targetDir;
            }
            public string HostUrl {  get { return this.hostUrl; } }
            public string TargetDir { get { return this.targetDir; } }
        }

        #region Test helper methods

        /// <summary>
        /// Event raised when "TryUpdate" is called
        /// </summary>
        public EventHandler<UpdatingEventArgs> Updating;

        public bool TryUpdateReturnValue { get; set; }

        public bool VersionCheckReturnValue { get; set; }

        public string ExpectedHostUrl { get; set; }

        public string ExpectedTargetDir { get; set; }

        public string ExpectedVersionPath { get; set;  }

        public Version ExpectedVersion { get; set; }

        #endregion

        #region Checks

        public void AssertUpdateAttempted()
        {
            Assert.IsTrue(this.tryUpdateCalled, "Expecting IBuildUpdater.TryUpdate to be been called");
        }

        public void AssertUpdateNotAttempted()
        {
            Assert.IsFalse(this.tryUpdateCalled, "Not expecting IBuildUpdater.TryUpdate to be been called");
        }

        public void AssertVersionChecked()
        {
            Assert.IsTrue(this.checkVersionCalled, "Expecting IBuildUpdater.CheckBootstrapperVersion to be been called");
        }

        public void AssertVersionNotChecked()
        {
            Assert.IsFalse(this.checkVersionCalled, "Not expecting IBuildUpdater.CheckBootstrapperVersion to be been called");
        }

        public void AssertTargetsInjected()
        {
            Assert.IsTrue(this.injectTargetsCalled, "Expecting IBuildUpdater.InjectLoaderTargets to be been called");
        }

        public void AssertTargetsNotInstalled()
        {
            Assert.IsFalse(this.injectTargetsCalled, "Not expecting IBuildUpdater.InjectLoaderTargets to be been called");
        }


        #endregion

        #region IBuildAgentUpdater interface

        bool IBuildAgentUpdater.CheckBootstrapperApiVersion(string versionFilePath, Version bootstrapperVersion)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(versionFilePath), "versionFilePath should be supplied");

            if (!string.IsNullOrEmpty(this.ExpectedVersionPath))
            {
                Assert.AreEqual(this.ExpectedVersionPath, versionFilePath, "Unexpected version file path");
                Assert.AreEqual(this.ExpectedVersion, bootstrapperVersion, "Unexpected version");
            }

            this.checkVersionCalled = true;

            return this.VersionCheckReturnValue;
        }

        bool IBuildAgentUpdater.TryUpdate(string hostUrl, string targetDir, ILogger logger)
        {
            Assert.IsNotNull(logger, "Expecting a valid logger to be supplied");

            if (this.ExpectedHostUrl != null)
            {
                Assert.AreEqual(this.ExpectedHostUrl, hostUrl, "Unexpected host url");
                Assert.AreEqual(this.ExpectedTargetDir, targetDir, "Unexpected targetDir");
            }

            this.tryUpdateCalled = true;

            if (this.Updating != null)
            {
                this.Updating(this, new UpdatingEventArgs(hostUrl, targetDir));
            }

            return this.TryUpdateReturnValue;
        }

        void IBuildAgentUpdater.InstallLoaderTargets(ILogger logger)
        {
            this.injectTargetsCalled = true;
        }

        #endregion
   }
}
