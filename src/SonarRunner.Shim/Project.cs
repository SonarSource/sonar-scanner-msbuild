//-----------------------------------------------------------------------
// <copyright file="Project.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace SonarRunner.Shim
{
    public class Project
    {
        public string Name { get; private set; }
        public Guid Guid { get; private set; }
        public string MsBuildProject { get; private set; }
        public bool IsTest { get; private set; }
        public List<string> Files { get; private set; }
        public string FxCopReport { get; private set; }
        public string VisualStudioCodeCoverageReport { get; private set; }

        public Project(String name, Guid guid, String msBuildProject, bool isTest, List<string> files, string fxCopReport, string visualStudioCodeCoverageReport)
        {
            Name = name;
            Guid = guid;
            MsBuildProject = msBuildProject;
            IsTest = isTest;
            Files = files;
            FxCopReport = fxCopReport;
            VisualStudioCodeCoverageReport = visualStudioCodeCoverageReport;
        }

        public string GuidAsString()
        {
            return Guid.ToString("D", CultureInfo.InvariantCulture).ToUpperInvariant();
        }

        public string BaseDir()
        {
            return Path.GetDirectoryName(MsBuildProject);
        }

        public List<string> FilesToAnalyze()
        {
            var result = new List<string>();
            var baseDir = BaseDir();

            foreach (string file in Files)
            {
                if (IsInFolder(file, baseDir))
                {
                    result.Add(file);
                }
            }

            return result;
        }

        private static bool IsInFolder(string filePath, string folder)
        {
            // FIXME This test is not sufficient...
            return filePath.StartsWith(folder + Path.DirectorySeparatorChar);
        }
    }
}
