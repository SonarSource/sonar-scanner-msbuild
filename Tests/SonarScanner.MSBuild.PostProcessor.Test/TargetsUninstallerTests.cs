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
using System.IO;
using System.Runtime.InteropServices;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarScanner.MSBuild.PostProcessor;
using TestUtilities;

namespace MSBuild.SonarQube.Internal.PostProcess.Tests;

[TestClass]
public class TargetsUninstallerTests
{
    public TestContext TestContext { get; set; }

    [TestMethod]
    public void Ctor_Argument_Check()
    {
        Action action = () => new TargetsUninstaller(null);
        action.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("logger");
    }

    [TestMethod]
    public void DeleteFileWhenPresent()
    {
        var context = new UninstallContext(TestContext, true);
        context.UninstallTargets();
        File.Exists(context.TargetsFilePath).Should().BeFalse();
        context.Logger.AssertDebugLogged("Uninstalling target: " + context.TargetsFilePath);
    }

    [TestMethod]
    public void Log_MissingFile()
    {
        var context = new UninstallContext(TestContext, false);
        context.UninstallTargets();
        context.Logger.AssertDebugLogged(context.BinDir + $@"{Path.DirectorySeparatorChar}targets{Path.DirectorySeparatorChar}SonarQube.Integration.targets does not exist");
    }

    [TestCategory(TestCategories.NoLinux)]
    [TestCategory(TestCategories.NoMacOS)]
    [TestMethod]
    public void Log_OnIOException()
    {
        var context = new UninstallContext(TestContext, true);
        using (var fs = new FileStream(context.TargetsFilePath, FileMode.Open, FileAccess.Read, FileShare.None))    // Lock file
        {
            context.UninstallTargets();
        }
        File.Exists(context.TargetsFilePath).Should().BeTrue(); // Failed to delete
        context.Logger.AssertDebugLogged("Could not delete " + context.TargetsFilePath);
    }

    [TestMethod]
    public void Log_OnUnauthorizedAccessException()
    {
        var context = new UninstallContext(TestContext, true);
        var lockPath = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? context.TargetsFilePath : Path.GetDirectoryName(context.TargetsFilePath);
        File.SetAttributes(lockPath, FileAttributes.ReadOnly);   // Make readonly to cause UnauthorizedAccessException
        try
        {
            context.UninstallTargets();
        }
        finally
        {
            // Restore to prevent UT output from blocking
            File.SetAttributes(context.TargetsFilePath, FileAttributes.Normal);
        }
        File.Exists(context.TargetsFilePath).Should().BeTrue(); // Failed to delete
        context.Logger.AssertDebugLogged("Could not delete " + context.TargetsFilePath);
    }

    private class UninstallContext
    {
        public readonly TestLogger Logger;
        public readonly TargetsUninstaller Uninstaller;
        public readonly string BinDir;
        public readonly string TargetsFilePath;

        public UninstallContext(TestContext testContext, bool createFile)
        {
            BinDir = TestUtils.CreateTestSpecificFolderWithSubPaths(testContext, "bin");
            Logger = new TestLogger();
            Uninstaller = new TargetsUninstaller(Logger);
            if (createFile)
            {
                var targetsDir = Path.Combine(BinDir, "targets");
                Directory.CreateDirectory(targetsDir);
                TargetsFilePath = TestUtils.CreateEmptyFile(targetsDir, "SonarQube.Integration.targets");
            }
        }

        public void UninstallTargets() =>
            Uninstaller.UninstallTargets(BinDir);
    }

}
