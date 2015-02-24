//-----------------------------------------------------------------------
// <copyright file="BuildTaskConstants.cs" company="SonarSource SA and Microsoft Corporation">
//   (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace SonarMSBuild.Tasks
{
    public static class BuildTaskConstants
    {
        public const string ProjectInfoFileName = "ProjectInfo.xml";
        public const string CompileListFileName = "CompileList.txt";

        public const string ResultItemName = "AnalysisResult";
        public const string ResultMetadataIdProperty = "Id";
        public const string ResultMetadataLocationProperty = "Location";
    }
}
