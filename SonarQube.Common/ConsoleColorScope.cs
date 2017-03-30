//-----------------------------------------------------------------------
// <copyright file="ConsoleColorScope.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

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
