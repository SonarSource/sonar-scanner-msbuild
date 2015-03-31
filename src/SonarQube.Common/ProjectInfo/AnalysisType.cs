//-----------------------------------------------------------------------
// <copyright file="AnalysisType.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------


namespace SonarQube.Common
{
    /* If we move to a plug-in model (i.e. so handlers for new types of analyzers
       can be plugged in at runtime e.g. using MEF) then this enum would be removed.
       For the time being we are only supported a known set of analyzers.
    */

    /// <summary>
    /// Lists the known types of analyzers that are handled by the properties generator
    /// </summary>
    public enum AnalysisType
    {
        ManagedCompilerInputs,
        ContentFiles,
        FxCop,
        VisualStudioCodeCoverage
    }
}
