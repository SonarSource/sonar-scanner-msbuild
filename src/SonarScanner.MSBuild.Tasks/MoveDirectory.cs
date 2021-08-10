/*
 * SonarScanner for MSBuild
 * Copyright (C) 2016-2021 SonarSource SA
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
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace SonarScanner.MSBuild.Tasks
{
    /// <summary>
    /// MSBuild task that moves content of folder.
    /// </summary>
    public sealed class MoveDirectory : Task
    {
        /// <summary>
        /// Name of the source directory.
        /// </summary>
        public string SourceDirectory { get; set; }

        /// <summary>
        /// Name of the destination directory.
        /// </summary>
        public string DestinationDirectory { get; set; }

        public override bool Execute()
        {
            if (string.IsNullOrWhiteSpace(SourceDirectory))
            {
                Log.LogMessage(MessageImportance.Normal, Resources.MoveDirectory_InvalidSourceDirectory, SourceDirectory);
                return false;
            }

            if (string.IsNullOrWhiteSpace(DestinationDirectory))
            {
                Log.LogMessage(MessageImportance.Normal, Resources.MoveDirectory_InvalidDestinationDirectory, DestinationDirectory);
                return false;
            }

            Directory.Move(SourceDirectory, DestinationDirectory);
            return true;
        }
    }
}
