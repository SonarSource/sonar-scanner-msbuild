//-----------------------------------------------------------------------
// <copyright file="EnvironmentVariableScope.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;

namespace TestUtilities
{
    /// <summary>
    /// Defines a scope inside which new environment variables can be set.
    /// The variables will be clear when the scope is disposed.
    /// </summary>
    public class EnvironmentVariableScope : IDisposable
    {
        private string originalPath;

        private IList<string> addedVars = new List<string>();

        public void AddVariable(string name, string value)
        {
            AssertEnvironmentVariableDoesNotExist(name);
            Environment.SetEnvironmentVariable(name, value, EnvironmentVariableTarget.Process);
            addedVars.Add(name);
        }

        public void SetPath(string value)
        {
            if (originalPath == null)
            {
                this.originalPath = Environment.GetEnvironmentVariable("PATH");
                Debug.Assert(this.originalPath != null, "Not expecting the path variable to be null");
            }

            Environment.SetEnvironmentVariable("PATH", value, EnvironmentVariableTarget.Process);
        }

        private static void AssertEnvironmentVariableDoesNotExist(string name)
        {
            IDictionary vars = Environment.GetEnvironmentVariables();
            Assert.IsFalse(vars.Contains(name), "Test setup error: environment variable already exists. Name: {0}", name);
        }

        public void Dispose()
        {
            this.Dispose(true);
        }

        private void Dispose(bool disposing)
        {
            if (disposing && this.addedVars != null)
            {
                foreach (string name in addedVars)
                {
                    Environment.SetEnvironmentVariable(name, null, EnvironmentVariableTarget.Process);
                    AssertEnvironmentVariableDoesNotExist(name);
                }
                this.addedVars = null;
            }

            // Restore the original path
            if (this.originalPath != null)
            {
                this.SetPath(this.originalPath);
                this.originalPath = null;
            }
        }
    }
}
