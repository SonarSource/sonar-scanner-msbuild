/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2024 SonarSource SA
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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using SonarScanner.MSBuild.Common;
using SonarScanner.MSBuild.Shim.Interfaces;

namespace SonarScanner.MSBuild.Shim;

public class AdditionalFilesService(IDirectoryWrapper directoryWrapper) : IAdditionalFilesService
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(100);

    private static readonly List<string> SupportedLanguages =
    [
        "sonar.tsql.file.suffixes",
        "sonar.plsql.file.suffixes",
        "sonar.yaml.file.suffixes",
        "sonar.xml.file.suffixes",
        "sonar.json.file.suffixes",
        "sonar.css.file.suffixes",
        "sonar.html.file.suffixes",
        "sonar.javascript.file.suffixes",
        "sonar.typescript.file.suffixes"
    ];

    public IEnumerable<string> AdditionalFiles(AnalysisConfig analysisConfig, DirectoryInfo projectBaseDir)
    {
        var extensions = SupportedLanguages
            .Select(x => analysisConfig.ServerSettings.Find(property => property.Id == x))
            .Where(x => x is not null)
            .SelectMany(x => x.Value.Split(','));

        if (extensions.Any())
        {
            var pattern = string.Join("|", extensions.Select(x => x.TrimStart('.') + "$").Distinct());
            var regex = new Regex(pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase, RegexTimeout);
            return directoryWrapper.EnumerateFiles(projectBaseDir.FullName, "*", SearchOption.AllDirectories).Where(regex.SafeIsMatch);
        }
        else
        {
            return [];
        }
    }
}
