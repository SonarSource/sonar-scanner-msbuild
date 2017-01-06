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
