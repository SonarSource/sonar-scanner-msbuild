//-----------------------------------------------------------------------
// <copyright file="WorkingDirectoryScope.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;

namespace TestUtilities
{
    /// <summary>
    /// Defines a scope inside which the current directory is changed
    /// to a specific value. The directory will be reset when the scope is disposed.
    /// </summary>
    /// <remarks>The location for the temporary analysis directory is based on the working directory.
    /// This class provides a simple way to set the directory to a known location for the duration
    /// of a test.</remarks>
    public class WorkingDirectoryScope : IDisposable
    {
        private readonly string originalDirectory;

        public WorkingDirectoryScope(string workingDirectory)
        {
            Assert.IsTrue(Directory.Exists(workingDirectory), "Test setup error: specified directory should exist - " + workingDirectory);

            this.originalDirectory = Directory.GetCurrentDirectory();
            Directory.SetCurrentDirectory(workingDirectory);
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
                Directory.SetCurrentDirectory(this.originalDirectory);
            }
        }

        #endregion
    }
}
