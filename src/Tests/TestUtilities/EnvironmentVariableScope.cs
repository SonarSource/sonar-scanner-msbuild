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

namespace TestUtilities
{
    /// <summary>
    /// Defines a scope inside which new environment variables can be set.
    /// The variables will be cleared when the scope is disposed.
    /// </summary>
    public class EnvironmentVariableScope : IDisposable
    {
        private IDictionary<string, string> originalValues = new Dictionary<string, string>();

        public void AddVariable(string name, string value)
        {
            AssertEnvironmentVariableDoesNotExist(name);
            SetVariable(name, value);
        }

        public void SetVariable(string name, string value)
        {
            // Store the original value, or null if there isn't one
            if (!this.originalValues.ContainsKey(name))
            {
                originalValues.Add(name, Environment.GetEnvironmentVariable(name));
            }
            Environment.SetEnvironmentVariable(name, value, EnvironmentVariableTarget.Process);
        }

        public void SetPath(string value)
        {
            SetVariable("PATH", value);
        }

        private static void AssertEnvironmentVariableDoesNotExist(string name)
        {
            IDictionary vars = Environment.GetEnvironmentVariables();
            Assert.IsFalse(vars.Contains(name), "Test setup error: environment variable already exists. Name: {0}", name);
        }

        #region IDispose implementation

        private bool disposed;

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (this.disposed)
            {
                return;
            }
            this.disposed = true;

            if (disposing)
            {
                if (this.originalValues != null)
                {
                    foreach(KeyValuePair<string, string> kvp in this.originalValues)
                    {
                        Environment.SetEnvironmentVariable(kvp.Key, kvp.Value);
                    }
                    this.originalValues = null;
                }
            }
        }

        #endregion
    }
}
