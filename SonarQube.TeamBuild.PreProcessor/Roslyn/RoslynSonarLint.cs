//-----------------------------------------------------------------------
// <copyright file="RoslynSonarLint.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using SonarQube.TeamBuild.PreProcessor.Roslyn.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SonarQube.TeamBuild.PreProcessor.Roslyn
{
    static class RoslynSonarLint
    {
        public static string generateXml(IEnumerable<ActiveRule> activeRules, string repoKey)
        {
            activeRules = activeRules.Where(ar => repoKey.Equals(ar.RepoKey));

            StringBuilder builder = new StringBuilder();
            builder.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
            builder.AppendLine("<AnalysisInput>");

            builder.AppendLine("  <Rules>");

            foreach (ActiveRule activeRule in activeRules)
            {
                builder.AppendLine("    <Rule>");
                string templateKey = activeRule.TemplateKey;
                String ruleKey = templateKey == null ? activeRule.RuleKey : templateKey;
                builder.AppendLine("      <Key>" + escapeXml(ruleKey) + "</Key>");

                if (activeRule.Parameters != null && activeRule.Parameters.Any())
                {
                    builder.AppendLine("      <Parameters>");
                    foreach (KeyValuePair<string, string> entry in activeRule.Parameters)
                    {
                        builder.AppendLine("        <Parameter>");
                        builder.AppendLine("          <Key>" + escapeXml(entry.Key) + "</Key>");
                        builder.AppendLine("          <Value>" + escapeXml(entry.Value) + "</Value>");
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

        private static String escapeXml(String str)
        {
            return str.Replace("&", "&amp;").Replace("\"", "&quot;").Replace("'", "&apos;").Replace("<", "&lt;").Replace(">", "&gt;");
        }
    }
}
