/*
 * SonarQube Scanner for MSBuild
 * Copyright (C) 2016-2018 SonarSource SA
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
using SonarScanner.MSBuild.Common;

namespace SonarScanner.MSBuild.PreProcessor
{
    /// <summary>
    /// Default implementation of the object factory interface that returns the
    /// product implementations of the required classes
    /// </summary>
    /// <remarks>
    /// Note: the factory is stateful and expects objects to be requested in the
    /// order they are used
    /// </remarks>
    public class PreprocessorObjectFactory : IPreprocessorObjectFactory
    {
        /// <summary>
        /// Reference to the SonarQube server to query
        /// </summary>
        /// <remarks>Cannot be constructed at runtime until the command line arguments have been processed.
        /// Once it has been created, it is stored so the factory can use the same instance when
        /// constructing the analyzer provider</remarks>
        private ISonarQubeServer server;

        #region IPreprocessorObjectFactory methods

        public ISonarQubeServer CreateSonarQubeServer(ProcessedArgs args, ILogger logger)
        {
            if (args == null)
            {
                throw new ArgumentNullException(nameof(args));
            }
            if (logger == null)
            {
                throw new ArgumentNullException(nameof(logger));
            }

            var username = args.GetSetting(SonarProperties.SonarUserName, null);
            var password = args.GetSetting(SonarProperties.SonarPassword, null);
            var hostUrl = args.SonarQubeUrl;

            server = new SonarWebService(new WebClientDownloader(username, password, logger), hostUrl, logger);
            return server;
        }

        public ITargetsInstaller CreateTargetInstaller(ILogger logger)
        {
            return new TargetsInstaller(logger);
        }

        public IAnalyzerProvider CreateRoslynAnalyzerProvider(ILogger logger)
        {
            if (logger == null)
            {
                throw new ArgumentNullException(nameof(logger));
            }

            if (server == null)
            {
                throw new InvalidOperationException(Resources.FACTORY_InternalError_MissingServer);
            }

            return new Roslyn.RoslynAnalyzerProvider(new Roslyn.EmbeddedAnalyzerInstaller(server, logger), logger);
        }

        #endregion IPreprocessorObjectFactory methods
    }
}
