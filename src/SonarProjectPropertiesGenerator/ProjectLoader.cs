using Sonar.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace SonarProjectPropertiesGenerator
{
    public static class ProjectLoader
    {
        public static List<Project> LoadFrom(string dumpFolderPath)
        {
            List<Project> result = new List<Project>();

            foreach (string projectFolderPath in Directory.GetDirectories(dumpFolderPath))
            {
                string projectInfoPath = Path.Combine(projectFolderPath, "ProjectInfo.xml");
                string compileListPath = Path.Combine(projectFolderPath, "CompileList.txt");

                if (!File.Exists(projectInfoPath) || !File.Exists(compileListPath))
                {
                    continue;
                }

                var projectInfo = ProjectInfo.Load(projectInfoPath);
                bool isTest = projectInfo.ProjectType == ProjectType.Test;
                
                List<String> files = File.ReadAllLines(compileListPath, Encoding.UTF8).ToList();

                result.Add(new Project(projectInfo.ProjectName, projectInfo.ProjectGuid, projectInfo.FullPath, isTest, files));
            }

            return result;
        }
    }
}
