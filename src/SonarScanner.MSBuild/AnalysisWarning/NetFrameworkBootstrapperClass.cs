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

#if NETFRAMEWORK
using System.IO;
using SonarScanner.MSBuild.Common;
using SonarScanner.MSBuild.Common.Interfaces;

namespace SonarScanner.MSBuild.AnalysisWarning
{
    public class NetFrameworkBootstrapperClass : BootstrapperClass
    {
        private readonly IFrameworkVersionProvider frameworkVersionProvider;

        public NetFrameworkBootstrapperClass(IProcessorFactory processorFactory, IBootstrapperSettings bootstrapSettings, ILogger logger)
            : this(processorFactory, bootstrapSettings, logger, new FrameworkVersionProvider()) { }

        public NetFrameworkBootstrapperClass(IProcessorFactory processorFactory, IBootstrapperSettings bootstrapSettings, ILogger logger, IFrameworkVersionProvider frameworkVersionProvider)
            : base(processorFactory, bootstrapSettings, logger) =>
            this.frameworkVersionProvider = frameworkVersionProvider;

        protected override void WarnAboutDeprecation(ITeamBuildSettings teamBuildSettings)
        {
            if (frameworkVersionProvider.IsLowerThan462FrameworkVersion())
            {
                const string netframework46Warning =
                    "From the 6th of July 2022, new versions of this scanner will no longer support .NET framework runtime environments less than .NET Framework 4.6.2." +
                    " For more information see https://community.sonarsource.com/t/54684";
                WarningsSerializer.Serialize(
                    new[] { new Warning(netframework46Warning) },
                    Path.Combine(teamBuildSettings.SonarOutputDirectory, "AnalysisWarnings.Scanner.json"));
            }
        }
    }
}
#endif
