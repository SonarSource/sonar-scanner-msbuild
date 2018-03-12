/*
 * SonarQube Scanner for MSBuild
 * Copyright (C) 2016-2018 SonarSource SA
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

using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

using Mutex = System.Threading.Mutex;

namespace SonarScanner.MSBuild.Tasks
{
    public class MakeUniqueDir : Task
    {
        /// <summary>
        /// The path where folder will be created, usually that is .sonarqube/out in the solution root
        /// </summary>
        [Required]
        public string Path { get; set; }

        /// <summary>
        /// Returns the name of the created folder
        /// </summary>
        [Output]
        public string UniqueName { get; private set; }

        /// <summary>
        /// Returns the full path of the created folder
        /// </summary>
        [Output]
        public string UniquePath { get; private set; }

        public override bool Execute()
        {
            var mutex = new Mutex(false, StripReservedChars(Path));

            mutex.WaitOne();

            try
            {
                for (var i = 0; /* endless */ ; i++)
                {
                    UniqueName = i.ToString();
                    UniquePath = System.IO.Path.Combine(Path, UniqueName);

                    if (!Directory.Exists(UniquePath))
                    {
                        Directory.CreateDirectory(UniquePath);
                        break;
                    }
                }
            }
            finally
            {
                mutex.ReleaseMutex();
            }

            return true;
        }

        /// <summary>
        /// It seems that the Mutex is using its name property to create a file somewhere,
        /// hence the path must be stripped from reserved characters. We should be generally
        /// safe on the length side, because the Mutex accepts names up to 260 chars which
        /// is already too long for file and folder names, e.g. if you have longer path here,
        /// you have bigger problems.
        /// </summary>
        private static string StripReservedChars(string path)
        {
            foreach (var c in System.IO.Path.GetInvalidFileNameChars())
            {
                path = path.Replace(c.ToString(), string.Empty);
            }
            return path;
        }
    }
}
