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

using System;
using System.Diagnostics;
using System.Linq;

namespace TestUtilities
{
    /// <summary>
    /// Helper class to suppress assertions during tests
    /// </summary>
    /// <remarks>Prevents tests from failing due to assertion dialogues appearing</remarks>
    public class AssertIgnoreScope : IDisposable
    {
        public AssertIgnoreScope()
        {
            SetAssertUIEnabled(false);
        }

        private static void SetAssertUIEnabled(bool enable)
        {
            DefaultTraceListener listener = Debug.Listeners.OfType<DefaultTraceListener>().FirstOrDefault();
            Debug.Assert(listener != null, "Failed to locate the default trace listener");
            if (listener != null)
            {
                listener.AssertUiEnabled = enable;
            }
        }

        #region IDisposable Support

        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    SetAssertUIEnabled(true);
                }

                disposedValue = true;
            }
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
        }

        #endregion
    }
}
