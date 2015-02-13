using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Xml.Linq;

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

                var projectInfo = XDocument.Load(projectInfoPath).Root;

                var name = SingleElement("ProjectName", projectInfo);
                var guid = Guid.Parse(SingleElement("ProjectGuid", projectInfo));
                var isTest = "Test".Equals(SingleElement("ProjectType", projectInfo));
                var msBuildProject = SingleElement("FullPath", projectInfo);;

                List<String> files = File.ReadAllLines(compileListPath, Encoding.UTF8).ToList();

                result.Add(new Project(name, guid, msBuildProject, isTest, files));
            }

            return result;
        }

        private static string SingleElement(string Name, XElement node)
        {
            return node.Elements(XName.Get(Name, "http://www.sonarsource.com/msbuild/integration/2015/1")).Single().Value;
        }
    }
}
