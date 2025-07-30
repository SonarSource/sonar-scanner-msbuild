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

using SonarScanner.MSBuild.PreProcessor.Roslyn;

namespace SonarScanner.MSBuild.PreProcessor.Test;

[TestClass]
public class PluginResourceCacheTests
{
    public TestContext TestContext { get; set; }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    public void Constructor_NullOrWhiteSpaceCacheDirectory_ThrowsArgumentNullException(string basedir) =>
        ((Action)(() => new PluginResourceCache(basedir))).Should().Throw<ArgumentNullException>().And.ParamName.Should().Be("basedir");

    [TestMethod]
    public void Constructor_NonExistentDirectory_ThrowsDirectoryNotFoundException() =>
        ((Action)(() => new PluginResourceCache("nonExistent"))).Should().Throw<DirectoryNotFoundException>().WithMessage("no such directory: nonExistent");

    [TestMethod]
    public void GetResourceSpecificDir_FolderAlreadyExistsWith0Name_CreatesOtherUniqueFolder()
    {
        var localCacheDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        var plugin = new Plugin() { Key = "plugin", Version = "1.0", StaticResourceName = "pluginResource" };
        var sut = new PluginResourceCache(localCacheDir);
        var alreadyExistingDirectory = Path.Combine(localCacheDir, "0");
        Directory.CreateDirectory(alreadyExistingDirectory);
        var plugin1ReosurceDir = sut.GetResourceSpecificDir(plugin);
        plugin1ReosurceDir.Should().NotBe(alreadyExistingDirectory);
    }
}
