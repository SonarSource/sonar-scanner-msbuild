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
using System.Linq;
using SonarScanner.MSBuild.Common;

namespace SonarScanner.MSBuild.TFS.Classic;

public class CommandLineArgs
{
    private readonly ILogger logger;
    public string SonarQubeAnalysisConfigPath { get; set; }

    public string SonarProjectPropertiesPath { get; set; }

    public Method ProcessToExecute { get; set; }
    public bool RanToCompletion { get; set; }

    public CommandLineArgs(ILogger logger)
    {
        this.logger = logger;
    }

    public bool ParseArguments(string[] args)
    {
        try
        {
            ProcessToExecute = (Method)Enum.Parse(typeof(Method), args[0], false);
            SonarQubeAnalysisConfigPath = args[1];
            SonarProjectPropertiesPath = args[2];
            RanToCompletion = args.Count() > 3 && bool.Parse(args[3]);
            return true;
        }
        catch
        {
            logger.LogError("Failed to parse or retrieve arguments for command line.");
            return false;
        }
    }
}
