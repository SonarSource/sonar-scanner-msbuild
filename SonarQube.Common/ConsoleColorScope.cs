/*
 * SonarQube Scanner for MSBuild
 * Copyright (C) 2016-2017 SonarSource SA
 * mailto:info AT sonarsource DOT com
 *
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with this program; if not, write to the Free Software Foundation,
 * Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.
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
