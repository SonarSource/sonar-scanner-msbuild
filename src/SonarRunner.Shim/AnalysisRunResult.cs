//-----------------------------------------------------------------------
// <copyright file="AnalysisRunResult.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using SonarQube.Common;
using System.Collections.Generic;

namespace SonarRunner.Shim
{
    public class AnalysisRunResult
    {
        public IDictionary<ProjectInfo, ProcessingStatus> Projects { get; set; }

        public bool RanToCompletion { get; set; }

        public string FullPropertiesFilePath { get; set; }
    }
}
