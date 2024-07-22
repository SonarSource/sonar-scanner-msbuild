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

using System.IO;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using SonarScanner.MSBuild.Common;

namespace SonarScanner.MSBuild.Shim.Test;

[TestClass]
public class AdditionalFilesServiceTest
{
    private readonly IDirectoryWrapper wrapper;
    private readonly DirectoryInfo directoryInfo;
    private readonly AdditionalFilesService sut;

    public AdditionalFilesServiceTest()
    {
        wrapper = Substitute.For<IDirectoryWrapper>();
        sut = new(wrapper);
        directoryInfo = new DirectoryInfo(Directory.GetCurrentDirectory());
    }

    [TestMethod]
    public void AdditionalFiles_NoExtensionsFound_ReturnsEmpty()
    {
        var files = sut.AdditionalFiles(new() { ServerSettings = [] }, directoryInfo);

        files.Should().BeEmpty();
        wrapper.DidNotReceive().EnumerateFiles(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<SearchOption>());
    }

    [DataTestMethod]
    [DataRow("sonar.tsql.file.suffixes")]
    [DataRow("sonar.plsql.file.suffixes")]
    [DataRow("sonar.yaml.file.suffixes")]
    [DataRow("sonar.xml.file.suffixes")]
    [DataRow("sonar.json.file.suffixes")]
    [DataRow("sonar.css.file.suffixes")]
    [DataRow("sonar.html.file.suffixes")]
    [DataRow("sonar.javascript.file.suffixes")]
    [DataRow("sonar.typescript.file.suffixes")]
    public void AdditionalFiles_ExtensionsFound_SingleProperty_ReturnsFiles(string propertyName)
    {
        wrapper.EnumerateFiles(directoryInfo.FullName, "*", SearchOption.AllDirectories).Returns(["valid.sql", "valid.js", "invalid.cs"]);

        var files = sut.AdditionalFiles(new() { ServerSettings = [new(propertyName, ".sql,.js")] }, directoryInfo);

        files.Should().BeEquivalentTo(["valid.sql", "valid.js"]);
    }

    [TestMethod]
    public void AdditionalFiles_ExtensionsFound_MultipleProperties_ReturnsFiles()
    {
        wrapper.EnumerateFiles(directoryInfo.FullName, "*", SearchOption.AllDirectories).Returns(["valid.html", "valid.sql", "invalid.js"]);
        var analysisConfig = new AnalysisConfig
        {
            ServerSettings =
            [
                new("sonar.html.file.suffixes", ".html"),
                new("sonar.tsql.file.suffixes", ".sql"),
            ]
        };

        var files = sut.AdditionalFiles(analysisConfig, directoryInfo);

        files.Should().BeEquivalentTo(["valid.html", "valid.sql"]);
    }
}
