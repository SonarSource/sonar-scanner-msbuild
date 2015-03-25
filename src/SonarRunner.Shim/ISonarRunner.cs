//-----------------------------------------------------------------------
// <copyright file="ISonarRunner.cs" company="SonarSource SA and Microsoft Corporation">
//   (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

using Sonar.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SonarRunner.Shim
{
    /// <summary>
    /// Encapsulate the interaction with the sonar-runner
    /// </summary>
    /// <remarks>Accepts the ProjectInfo.xml files as input, generates a sonar-runner.properties 
    /// file from them, then executes the Java sonar-runnerr.</remarks>
    public interface ISonarRunner
    {
        bool Execute(AnalysisConfig config, ILogger logger);
    }

    public class AnalysisRunResult
    {
        public bool Succeeded { get; private set; }

        public IEnumerable<string> ProductProjects { get; private set; }

        public IEnumerable<string> TestProjects { get; private set; }

        public IEnumerable<string> ExcludedProjects { get; private set; }

        /// <summary>
        /// A project will be skipped if it cannot be mapped to a module in Sonar
        /// i.e. if it has a missing or duplicate ProjectGuid
        /// </summary>
        public IEnumerable<string> SkippedProjects { get; private set; }

    }
}
