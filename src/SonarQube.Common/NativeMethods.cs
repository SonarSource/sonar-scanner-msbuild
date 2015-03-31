//-----------------------------------------------------------------------
// <copyright file="NativeMethods.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace SonarQube.Common
{
    /// <summary>
    /// Container for all native dll definitions and calls
    /// </summary>
    internal static class NativeMethods
    {
        public const int MAXPATH = 260; // maximum length of path in Windows

        //BOOL PathFindOnPath(_Inout_   LPTSTR pszFile, _In_opt_  LPCTSTR *ppszOtherDirs)
        [DllImport("Shlwapi.dll", CharSet = CharSet.Auto)]
        private static extern bool PathFindOnPath([In, Out] StringBuilder fileName, [In]string[] otherDirs);

        #region Public methods

        /// <summary>
        /// Searches the directories defined in the PATH environment variable for the specified file
        /// </summary>
        /// <param name="fileName">The name of the file</param>
        /// <returns>The full path to the file, or null if the file could not be located</returns>
        public static string FindOnPath(string fileName)
        {
            if (fileName == null)
            {
                throw new ArgumentNullException("fileName");
            }

            Debug.Assert(fileName.Equals(Path.GetFileName(fileName)), "Parameter should be a file name i.e. should not include any path elements");

            StringBuilder sb = new StringBuilder(fileName, MAXPATH);

            bool found = PathFindOnPath(sb, null);
            if (found)
            {
                return sb.ToString();
            }
            return null;
        }

        #endregion
    }
}
