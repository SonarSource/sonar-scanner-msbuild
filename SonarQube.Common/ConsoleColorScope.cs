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

namespace SonarQube.Common
{
    /// <summary>
    /// Utility class that changes the console text colour for the lifetime of the instance
    /// </summary>
    /// <remarks>This will have no effect if the console output streams have been re-directed</remarks>
    internal class ConsoleColorScope : IDisposable
    {
        private readonly ConsoleColor originalForeground;
        private readonly ConsoleColor originalBackground;

        public ConsoleColorScope(ConsoleColor textColor)
        {
            this.originalForeground = Console.ForegroundColor;
            this.originalBackground = Console.BackgroundColor;

            // Check the text doesn't clash with the background color
            ConsoleColor newBackground = Console.BackgroundColor;
            if (textColor == Console.BackgroundColor)
            {
                newBackground = (newBackground == ConsoleColor.Black) ? ConsoleColor.Gray : ConsoleColor.Black;
            }
            SetColors(textColor, newBackground);
        }

        private static void SetColors(ConsoleColor foreground, ConsoleColor background)
        {
            try
            {
                if (Console.ForegroundColor != foreground)
                {
                    Console.ForegroundColor = foreground;
                }
                if (Console.BackgroundColor != background)
                {
                    Console.BackgroundColor = background;
                }
            }
            catch (System.IO.IOException)
            {
                // Swallow the exception: no point in failing if we can't set the color
                Debug.WriteLine("Failed to set the console color");
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
                    SetColors(this.originalForeground, this.originalBackground);
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
