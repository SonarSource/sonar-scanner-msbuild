//-----------------------------------------------------------------------
// <copyright file="IRulesetGenerator.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using SonarQube.TeamBuild.PreProcessor.Roslyn.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SonarQube.TeamBuild.PreProcessor.Interfaces
{
    public interface IRulesetGenerator
    {
        void Generate(string fxCopRepositoryKey, IList<ActiveRule> activeRules, string outputFilePath);
    }
}
