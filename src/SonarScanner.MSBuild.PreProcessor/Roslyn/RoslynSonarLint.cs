/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2025 SonarSource SA
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

using System.Collections.Generic;
using System.Linq;
using System.Text;
using SonarScanner.MSBuild.Common;
using SonarScanner.MSBuild.PreProcessor.Roslyn.Model;

namespace SonarScanner.MSBuild.PreProcessor.Roslyn;

internal static class RoslynSonarLint
{
    public static string GenerateXml(IEnumerable<SonarRule> activeRules, IAnalysisPropertyProvider analysisProperties,
        string language)
    {
        var repoKey = language.Equals(RoslynAnalyzerProvider.CSharpLanguage) ? "csharpsquid" : "vbnet";

        var repoActiveRules = activeRules.Where(ar => repoKey.Equals(ar.RepoKey));
        var settings = analysisProperties.GetAllProperties().Where(a => a.Id.StartsWith("sonar." + language + "."));

        var builder = new StringBuilder();
        builder.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        builder.AppendLine("<AnalysisInput>");

        builder.AppendLine("  <Settings>");
        foreach (var pair in settings)
        {
            if (!Utilities.IsSecuredServerProperty(pair.Id))
            {
                WriteSetting(builder, pair.Id, pair.Value);
            }
        }
        builder.AppendLine("  </Settings>");

        builder.AppendLine("  <Rules>");

        foreach (var activeRule in repoActiveRules)
        {
            builder.AppendLine("    <Rule>");
            var templateKey = activeRule.TemplateKey;
            var ruleKey = templateKey ?? activeRule.RuleKey;
            builder.AppendLine("      <Key>" + EscapeXml(ruleKey) + "</Key>");

            if (activeRule.Parameters != null && activeRule.Parameters.Any())
            {
                builder.AppendLine("      <Parameters>");
                foreach (var entry in activeRule.Parameters)
                {
                    builder.AppendLine("        <Parameter>");
                    builder.AppendLine("          <Key>" + EscapeXml(entry.Key) + "</Key>");
                    builder.AppendLine("          <Value>" + EscapeXml(entry.Value) + "</Value>");
                    builder.AppendLine("        </Parameter>");
                }
                builder.AppendLine("      </Parameters>");
            }
            builder.AppendLine("    </Rule>");
        }

        builder.AppendLine("  </Rules>");

        builder.AppendLine("  <Files>");
        builder.AppendLine("  </Files>");
        builder.AppendLine("</AnalysisInput>");

        return builder.ToString();
    }

    private static void WriteSetting(StringBuilder builder, string key, string value)
    {
        builder.AppendLine("    <Setting>");
        builder.AppendLine("      <Key>" + EscapeXml(key) + "</Key>");
        builder.AppendLine("      <Value>" + EscapeXml(value) + "</Value>");
        builder.AppendLine("    </Setting>");
    }

    private static string EscapeXml(string str)
    {
        return str.Replace("&", "&amp;").Replace("\"", "&quot;").Replace("'", "&apos;").Replace("<", "&lt;").Replace(">", "&gt;");
    }
}
