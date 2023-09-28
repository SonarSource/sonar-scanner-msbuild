/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2023 SonarSource SA
 * mailto: info AT sonarsource DOT com
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
using System.ComponentModel;
using System.Diagnostics;
using System.Threading.Tasks;
using SonarScanner.MSBuild.Common;

namespace SonarScanner.MSBuild.PreProcessor
{
    public class JavaExecutableOptionVersion : IJavaVersion
    {
        private const string JavaExe = "java";

        private readonly ILogger logger;

        public JavaExecutableOptionVersion(ILogger logger)
        {
            this.logger = logger;
        }

        public async Task<Version> GetVersionAsync()
        {
            try
            {
                var process = new Process();
                process.StartInfo.FileName = JavaExe;
                process.StartInfo.Arguments = "--version";
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.UseShellExecute = false;
                process.Start();

                var versionLine = await process.StandardOutput.ReadLineAsync();

                var version = versionLine.Split(' ')[1];

                process.WaitForExit();

                return Version.Parse(version);
            }
            catch (Win32Exception exception)
            {
                logger.LogWarning(Resources.WARN_UnableToGetJavaVersion, exception.Message);
                return null;
            }
        }
    }
}
