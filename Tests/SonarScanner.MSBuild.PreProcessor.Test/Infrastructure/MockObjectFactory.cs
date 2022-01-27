/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2021 SonarSource SA
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

using FluentAssertions;

namespace SonarScanner.MSBuild.PreProcessor.Tests
{
    internal class MockObjectFactory : IPreprocessorObjectFactory
    {
        private readonly ISonarQubeServer server;
        private readonly IAnalyzerProvider analyzerProvider;
        private readonly ITargetsInstaller targetsInstaller;

        public MockObjectFactory(ISonarQubeServer server)
        {
            this.server = server;
        }

        public MockObjectFactory(ISonarQubeServer server, ITargetsInstaller targetsInstaller, IAnalyzerProvider analyzerProvider)
        {
            this.server = server;
            this.targetsInstaller = targetsInstaller;
            this.analyzerProvider = analyzerProvider;
        }

        #region PreprocessorObjectFactory methods

        public IAnalyzerProvider CreateRoslynAnalyzerProvider()
        {
            return this.analyzerProvider;
        }

        public ISonarQubeServer CreateSonarQubeServer(ProcessedArgs args)
        {
            args.Should().NotBeNull();

            return this.server;
        }

        public ITargetsInstaller CreateTargetInstaller()
        {
            return this.targetsInstaller;
        }

        #endregion PreprocessorObjectFactory methods
    }
}
