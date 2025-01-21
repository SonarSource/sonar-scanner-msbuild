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
using SonarScanner.MSBuild.Common;
using SonarScanner.MSBuild.PreProcessor.Interfaces;

namespace SonarScanner.MSBuild.PreProcessor.Unpacking;

public class UnpackerFactory : IUnpackerFactory
{
    public static UnpackerFactory Instance { get; } = new UnpackerFactory();

    public IUnpacker Create(ILogger logger, IDirectoryWrapper directoryWrapper, IFileWrapper fileWrapper, IFilePermissionsWrapper filePermissionsWrapper, string archivePath) =>
        archivePath switch
        {
            _ when archivePath.EndsWith(".ZIP", StringComparison.OrdinalIgnoreCase) => new ZipUnpacker(),
            _ when archivePath.EndsWith(".TAR.GZ", StringComparison.OrdinalIgnoreCase) => new TarGzUnpacker(logger, directoryWrapper, fileWrapper, filePermissionsWrapper),
            _ => null
        };
}
