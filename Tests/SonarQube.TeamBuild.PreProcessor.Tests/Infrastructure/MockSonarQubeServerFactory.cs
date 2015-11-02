//-----------------------------------------------------------------------
// <copyright file="MockSonarQubeServerFactory.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using System;

namespace SonarQube.TeamBuild.PreProcessor.Tests
{
    class MockSonarQubeServerFactory : ISonarQubeServerFactory
    {
        private readonly ISonarQubeServer server;

        public MockSonarQubeServerFactory(ISonarQubeServer server)
        {
            this.server = server;
        }

        public ISonarQubeServer Create(ProcessedArgs args)
        {
            if (args == null)
            {
                throw new ArgumentNullException("args");
            }
            return this.server;
        }
    }
}
