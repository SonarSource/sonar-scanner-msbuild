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
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SonarScanner.MSBuild.Common;

namespace SonarScanner.MSBuild.PreProcessor.Roslyn.Model
{
    public class GlobalAnalyzerConfigGenerator
    {
        private const string ActiveRuleText = "Warning";
        private const string InactiveRuleText = "None";

        private readonly bool deactivateAll;

        public GlobalAnalyzerConfigGenerator(bool deactivateAll = false)
        {
            this.deactivateAll = deactivateAll;
        }

        public string Generate(string language, IEnumerable<SonarRule> rules)
        {
            _ = language ?? throw new ArgumentNullException(nameof(language));
            _ = rules ?? throw new ArgumentNullException(nameof(rules));

            var globalAnalyzerConfig = new StringBuilder();
            globalAnalyzerConfig.Append(
                $@"# Top level entry required to mark this as a global AnalyzerConfig file
is_global = true
global_level = 100

dotnet_style_qualification_for_method = true:warning

# Rules
");
            var rulesElements = rules
                .Select(x => $"dotnet_diagnostic.{x.RuleKey}.severity = {Severity(x.IsActive)}");
            foreach (var rule in rulesElements)
            {
                globalAnalyzerConfig.AppendLine(rule);
            }

            return globalAnalyzerConfig.ToString();

            string Severity(bool isActive) => isActive && !deactivateAll ? ActiveRuleText : InactiveRuleText;
        }
    }
}
