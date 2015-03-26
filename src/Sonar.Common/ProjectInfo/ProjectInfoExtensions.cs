//-----------------------------------------------------------------------
// <copyright file="ProjectInfoExtensions.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Linq;

namespace Sonar.Common
{
    /// <summary>
    /// Extension methods for <see cref="ProjectInfo"/>
    /// </summary>
    public static class ProjectInfoExtensions
    {
        #region Public methods

        /// <summary>
        /// Attempts to find and return the analyzer result with the specified id
        /// </summary>
        /// <returns>True if the analyzer result was found, otherwise false</returns>
        public static bool TryGetAnalyzerResult(this ProjectInfo projectInfo, AnalysisType analyzerType, out AnalysisResult result)
        {
            return TryGetAnalyzerResult(projectInfo, analyzerType.ToString(), out result);
        }

        /// <summary>
        /// Attempts to find and return the analyzer result with the specified id
        /// </summary>
        /// <returns>True if the analyzer result was found, otherwise false</returns>
        public static bool TryGetAnalyzerResult(this ProjectInfo projectInfo, string id, out AnalysisResult result)
        {
            if (projectInfo == null)
            {
                throw new ArgumentNullException("projectInfo");
            }

            result = null;

            if (projectInfo.AnalysisResults != null)
            {
                result = projectInfo.AnalysisResults.FirstOrDefault(ar => id.Equals(ar.Id, StringComparison.InvariantCulture));
            }
            return result != null;
        }

        #endregion
    }
}
