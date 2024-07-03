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

using System.Threading.Tasks;

namespace SonarScanner.MSBuild.PreProcessor
{
    /// <summary>
    /// Factory that creates the various objects required by the pre-processor.
    /// </summary>
    public interface IPreprocessorObjectFactory
    {
        /// <summary>
        /// Creates the component that interacts with the Sonar server.
        /// </summary>
        /// <remarks>It is the responsibility of the caller to dispose of the server, if necessary.</remarks>
        Task<ISonarWebServer> CreateSonarWebServer(ProcessedArgs args, IDownloader downloader = null, IDownloader apiDownloader = null);

        /// <summary>
        /// Creates the component to install the MSBuild targets.
        /// </summary>
        ITargetsInstaller CreateTargetInstaller();

        /// <summary>
        /// Creates the component that provisions the Roslyn analyzers.
        /// </summary>
        IAnalyzerProvider CreateRoslynAnalyzerProvider(ISonarWebServer server, string localCacheTempPath);
    }
}
