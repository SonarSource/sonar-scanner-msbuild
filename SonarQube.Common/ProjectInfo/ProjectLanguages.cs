//-----------------------------------------------------------------------
// <copyright file="ProjectLanguages.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using System;
namespace SonarQube.Common
{
    public static class ProjectLanguages
    {
        /* These constants must match the values used in by the C# and VB standard targets*/

        public const string CSharp = "C#";
        public const string VisualBasic = "VB";


        private static StringComparer LanguageNameComparer = StringComparer.Ordinal;

        public static bool IsCSharpProject(string language)
        {
            return LanguageNameComparer.Equals(language, ProjectLanguages.CSharp);
        }

        public static bool IsVbProject(string language)
        {
            return LanguageNameComparer.Equals(language, ProjectLanguages.VisualBasic);
        }

    }
}