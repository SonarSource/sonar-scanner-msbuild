//-----------------------------------------------------------------------
// <copyright file="ISonarScanner.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using SonarQube.Common;
using System.Collections.Generic;

namespace SonarScanner.Shim
{
    /// <summary>
    /// Encapsulate the interaction with the sonar-scanner
    /// </summary>
    /// <remarks>Accepts the ProjectInfo.xml files as input, generates a sonar-scanner.properties 
    /// file from them, then executes the Java sonar-scanner</remarks>
    public interface ISonarScanner
    {
        ProjectInfoAnalysisResult Execute(AnalysisConfig config, IEnumerable<string> userCmdLineArguments, ILogger logger);
    }
}
