/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2022 SonarSource SA
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
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SonarScanner.MSBuild.Shim.IgnoredIssues
{
    public static class SarifIssueRemoverFactory
    {
        private const string Version = "version";

        public static void RemoveIgnoredIssuesFromJson(string jsonPath, ISet<string> sonarRuleIds)
        {
            var issuesJson = JObject.Parse(File.ReadAllText(jsonPath));
            if (!issuesJson.ContainsKey(Version))
            {
                return;
            }

            bool changed;
            switch (issuesJson.GetValue(Version).Value<string>())
            {
                case "0.1":
                    changed = SarifIssueRemover01.Filter(issuesJson, sonarRuleIds);
                    break;
                case "0.4":
                    changed = SarifIssueRemover04.Filter(issuesJson, sonarRuleIds);
                    break;
                case "1.0.0":
                    changed = SarifIssueRemover10.Filter(issuesJson, sonarRuleIds);
                    break;
                default:
                    return;
            }

            if (changed)
            {
                using (var file = File.CreateText(jsonPath))
                {
                    using (var writer = new JsonTextWriter(file))
                    {
                        writer.Formatting = Formatting.Indented;
                        issuesJson.WriteTo(writer);
                    }
                }
            }
        }
    }
}
