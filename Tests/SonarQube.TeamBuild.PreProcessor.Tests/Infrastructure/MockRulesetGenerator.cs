//-----------------------------------------------------------------------
// <copyright file="MockRulesetGenerator.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using SonarQube.TeamBuild.PreProcessor.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SonarQube.TeamBuild.PreProcessor.Roslyn.Model;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SonarQube.TeamBuild.PreProcessor.Tests.Infrastructure
{
    class MockRulesetGenerator : IRulesetGenerator
    {
        public string FileToReturn { get; set; }
        public void Generate(string fxCopRepositoryKey, IList<ActiveRule> activeRules, string outputFilePath)
        {
            Assert.IsNotNull(activeRules);
            Assert.IsFalse(string.IsNullOrWhiteSpace(fxCopRepositoryKey));

            outputFilePath = FileToReturn;
        }
    }
}
