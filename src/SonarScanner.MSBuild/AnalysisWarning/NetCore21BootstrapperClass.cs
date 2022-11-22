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

#if NETCOREAPP2_1
using System.IO;
using SonarScanner.MSBuild.Common;
using SonarScanner.MSBuild.Common.Interfaces;

namespace SonarScanner.MSBuild.AnalysisWarning
{
    public class NetCore21BootstrapperClass : BootstrapperClass
    {
        public NetCore21BootstrapperClass(IProcessorFactory processorFactory, IBootstrapperSettings bootstrapSettings, ILogger logger) : base(processorFactory, bootstrapSettings, logger)
        {
        }

        protected override void WarnAboutDeprecation(IBuildSettings teamBuildSettings)
        {
            const string netcore2Warning =
                "From the 6th of July 2022, we will no longer release new Scanner for .NET versions that target .NET Core 2.1." +
                " If you are using the .NET Core Global Tool you will need to use a supported .NET runtime environment." +
                " For more information see https://community.sonarsource.com/t/54684";
            WarningsSerializer.Serialize(
                new[] { new Warning(netcore2Warning) },
                Path.Combine(teamBuildSettings.SonarOutputDirectory, "AnalysisWarnings.Scanner.json"));
        }
    }
}
#endif
