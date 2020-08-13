/*
 * SonarScanner for MSBuild
 * Copyright (C) 2016-2019 SonarSource SA
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
using System.Collections.Generic;
using System.Threading.Tasks;
using SonarScanner.MSBuild.PreProcessor.Roslyn.Model;

namespace SonarScanner.MSBuild.PreProcessor
{
    /// <summary>
    /// Provides an abstraction for the interactions with the SonarQube server
    /// </summary>
    public interface ISonarQubeServer
    {
        Task<IList<SonarRule>> GetInactiveRules(string qprofile, string language);

        Task<IList<SonarRule>> GetActiveRules(string qprofile);

        /// <summary>
        /// Get all keys of all available languages
        /// </summary>
        Task<IEnumerable<string>> GetAllLanguages();

        /// <summary>
        /// Get all the properties of a project
        /// </summary>
        Task<IDictionary<string, string>> GetProperties(string projectKey, string projectBranch);

        /// <summary>
        /// Get the name of the quality profile (of the given language) to be used by the given project key
        /// </summary>
        Task<Tuple<bool, string>> TryGetQualityProfile(string projectKey, string projectBranch, string organization, string language);

        /// <summary>
        /// Attempts to download a file embedded in the "static" folder in a plugin jar
        /// </summary>
        /// <param name="pluginKey">The key of the plugin containing the file</param>
        /// <param name="embeddedFileName">The name of the file to download</param>
        /// <param name="targetDirectory">The directory to which the file should be downloaded</param>
        Task<bool> TryDownloadEmbeddedFile(string pluginKey, string embeddedFileName, string targetDirectory);

        Task<Version> GetServerVersion();
    }
}
