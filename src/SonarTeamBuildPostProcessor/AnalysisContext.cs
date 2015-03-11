//-----------------------------------------------------------------------
// <copyright file="AnalysisContext.cs" company="SonarSource SA and Microsoft Corporation">
//   (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

using Sonar.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SonarTeamBuildPostProcessor
{
    internal class AnalysisContext
    {
        public string TfsUri { get; set; }

        public string BuildUri { get; set; }

        public string SonarConfigDir { get; set; }

        public string SonarOutputDir { get; set; }

        public ILogger Logger { get; set; }
    }
}
