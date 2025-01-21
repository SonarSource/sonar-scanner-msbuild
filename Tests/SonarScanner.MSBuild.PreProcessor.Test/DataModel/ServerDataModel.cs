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

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using FluentAssertions;
using SonarScanner.MSBuild.PreProcessor.Roslyn.Model;
using TestUtilities;

namespace SonarScanner.MSBuild.PreProcessor.Test;

internal class ServerDataModel
{
    private readonly IList<QualityProfile> qualityProfiles;
    private readonly IDictionary<string, byte[]> embeddedFilesMap;

    public ServerDataModel()
    {
        this.qualityProfiles = new List<QualityProfile>();
        ServerProperties = new Dictionary<string, string>();
        Languages = new List<string>();
        this.embeddedFilesMap = new Dictionary<string, byte[]>();
        SonarQubeVersion = new Version(5, 6);
    }

    public IEnumerable<QualityProfile> QualityProfiles { get { return this.qualityProfiles; } }

    public IDictionary<string, string> ServerProperties { get; set; }

    public IList<string> Languages { get; set; }

    public Version SonarQubeVersion { get; set; }

    #region Builder methods

    public QualityProfile AddQualityProfile(string id, string language, string organization)
    {
        var profile = FindProfile(id);
        profile.Should().BeNull("A quality profile already exists. Id: {0}, language: {1}", id, language);

        profile = new QualityProfile(id, language, organization);
        this.qualityProfiles.Add(profile);
        return profile;
    }

    public void AddEmbeddedZipFile(string pluginKey, string embeddedFileName, params string[] contentFileNames)
    {
        this.embeddedFilesMap.Add(GetEmbeddedFileKey(pluginKey, embeddedFileName), CreateDummyZipFile(contentFileNames));
    }

    #endregion Builder methods

    #region Locator methods

    public QualityProfile FindProfile(string id)
    {
        var profile = this.qualityProfiles.SingleOrDefault(qp => string.Equals(qp.Id, id));
        return profile;
    }

    public byte[] FindEmbeddedFile(string pluginKey, string embeddedFileName)
    {
        this.embeddedFilesMap.TryGetValue(GetEmbeddedFileKey(pluginKey, embeddedFileName), out byte[] content);
        return content;
    }

    private static string GetEmbeddedFileKey(string pluginKey, string embeddedFileName)
    {
        return pluginKey + "___" + embeddedFileName;
    }

    #endregion Locator methods

    #region Private methods

    private byte[] CreateDummyZipFile(params string[] contentFileNames)
    {
        var fileName = "dummy.zip";

        // Create a temporary directory structure
        var tempDir = Path.Combine(Path.GetTempPath(), "sqTestsTemp", System.Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        var zipDir = Path.Combine(tempDir, "zipDir");
        Directory.CreateDirectory(zipDir);
        var zippedFilePath = Path.Combine(tempDir, fileName);

        // Create and read the zip file
        foreach (var contentFileName in contentFileNames)
        {
            TestUtils.CreateTextFile(zipDir, contentFileName, "dummy file content");
        }

        ZipFile.CreateFromDirectory(zipDir, zippedFilePath);
        var zipData = File.ReadAllBytes(zippedFilePath);

        // Cleanup
        Directory.Delete(tempDir, true);

        return zipData;
    }

    #endregion Private methods
}
