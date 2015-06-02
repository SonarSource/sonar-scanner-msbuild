//-----------------------------------------------------------------------
// <copyright file="AnalysisResult.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace SonarQube.Common
{
    /// <summary>
    /// Data class to describe the output of a single type of analyzer
    /// </summary>
    /// <remarks>The class is XML-serializable.
    /// Examples of types of analyzer: FxCop, StyleCode, CodeCoverage, Roslyn Analyzers...</remarks>
    public class AnalysisResult
    {
        #region Data

        /// <summary>
        /// The identifier for the type of analyzer
        /// </summary>
        /// <remarks>Each type </remarks>
        [XmlAttribute]
        public string Id { get; set; }

        /// <summary>
        /// The location of the output produced by the analyzer
        /// </summary>
        /// <remarks>This will normally be an absolute file path</remarks>
        [XmlAttribute]
        public string Location { get; set; }

        #endregion

        #region Static helpers

        /// <summary>
        /// Comparer to use when comparing keys of analysis results
        /// </summary>
        public static IEqualityComparer<string> ResultKeyComparer = StringComparer.Ordinal;

        #endregion


    }
}
