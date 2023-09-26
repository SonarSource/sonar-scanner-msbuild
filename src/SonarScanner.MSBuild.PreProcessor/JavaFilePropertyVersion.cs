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
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using SonarScanner.MSBuild.Common;

namespace SonarScanner.MSBuild.PreProcessor
{
    public class JavaFilePropertyVersion : IJavaVersion
    {
        private const string JavaExe = "java.exe";
        private const char WindowsPathSep = ';';

        private readonly ILogger logger;

        public JavaFilePropertyVersion(ILogger logger)
        {
            this.logger = logger;
        }

        public Task<Version> GetVersionAsync()
        {
            var javaExecutablePath = GetJavaAbsolutePath();

            if (javaExecutablePath is null)
            {
                logger.LogWarning(Resources.WARN_JavaNotFound);
                return Task.FromResult<Version>(null);
            }

            var versionInfo = FileVersionInfo.GetVersionInfo(javaExecutablePath);

            if (!Version.TryParse(versionInfo.FileVersion, out var version))
            {
                logger.LogWarning(Resources.WARN_UnableToGetJavaVersion, string.Format(Resources.MISC_InvalidVersionFormat, versionInfo.FileVersion));
            }
            return Task.FromResult(version);
        }

        private static string GetJavaAbsolutePath()
        {
            var envPath = Environment.GetEnvironmentVariable("PATH");

            if (envPath is null)
            {
                return null;
            }

            var javaPath = Array.Find(envPath.Split(WindowsPathSep), path => File.Exists(Path.Combine(path, JavaExe)));

            return javaPath is not null ? Path.Combine(javaPath, JavaExe) : null;
        }
    }
}
