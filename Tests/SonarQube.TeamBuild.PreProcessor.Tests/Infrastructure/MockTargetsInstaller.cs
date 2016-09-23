//-----------------------------------------------------------------------
// <copyright file="MockTargetsInstaller.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using SonarQube.Common;

namespace SonarQube.TeamBuild.PreProcessor.Tests
{
    public class MockTargetsInstaller : ITargetsInstaller
    {
        bool installLoaderTargetsCalled = false;

        #region Asserts
        public void AssertsTargetsCopied()
        {
            Assert.IsTrue(installLoaderTargetsCalled, "The loader targets were not copied");
        }

        public void AssertsTargetsNotCopied()
        {
            Assert.IsFalse(installLoaderTargetsCalled, "The loader targets were actually copied");
        }

        #endregion

        #region ITargetsInstaller

        public void InstallLoaderTargets(ILogger logger, string workDirectory)
        {
            installLoaderTargetsCalled = true;
        }

        #endregion
    }
}
