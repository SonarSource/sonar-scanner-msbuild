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
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using SonarScanner.MSBuild.Common;
using SonarScanner.MSBuild.PreProcessor.JreCaching;

namespace SonarScanner.MSBuild.PreProcessor.Test.JreCaching;

[TestClass]
public class UnpackerFactoryTests
{
    [DataTestMethod]
    [DataRow("File.zip", typeof(ZipUnpacker))]
    [DataRow("File.ZIP", typeof(ZipUnpacker))]
    [DataRow(@"c:\test\File.ZIP", typeof(ZipUnpacker))]
    [DataRow(@"/usr/File.zip", typeof(ZipUnpacker))]
    [DataRow("File.tar.gz", typeof(TarGzUnpacker))]
    [DataRow("File.TAR.GZ", typeof(TarGzUnpacker))]
    [DataRow(@"c:\test\File.GZ", typeof(TarGzUnpacker))]
    [DataRow(@"/usr/File.TAR.gz", typeof(TarGzUnpacker))]
    public void SupportedFileExtensions(string fileName, Type expectedUnpacker)
    {
        var sut = new UnpackerFactory();

        var unpacker = sut.Create(Substitute.For<IDirectoryWrapper>(), Substitute.For<IFileWrapper>(), Substitute.For<IOperatingSystemProvider>(), fileName);

        unpacker.Should().BeOfType(expectedUnpacker);
    }

    [DataTestMethod]
    [DataRow("File.rar")]
    [DataRow("File.7z")]
    public void UnsupportedFileExtensions(string fileName)
    {
        var sut = new UnpackerFactory();

        var unpacker = sut.Create(Substitute.For<IDirectoryWrapper>(), Substitute.For<IFileWrapper>(), Substitute.For<IOperatingSystemProvider>(), fileName);

        unpacker.Should().BeNull();
    }
}
