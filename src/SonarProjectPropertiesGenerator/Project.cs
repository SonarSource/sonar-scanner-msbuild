using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Globalization;

namespace SonarProjectPropertiesGenerator
{
    public class Project
    {
        public string Name { get; private set; }
        public Guid Guid { get; private set; }
        public string MsBuildProject { get; private set; }
        public bool IsTest { get; private set; }
        public List<string> Files { get; private set; }

        public Project(String name, Guid guid, String msBuildProject, bool isTest, List<string> files)
        {
            Name = name;
            Guid = guid;
            MsBuildProject = msBuildProject;
            IsTest = isTest;
            Files = files;
        }

        public string GuidAsString()
        {
            return Guid.ToString("D", CultureInfo.InvariantCulture).ToUpperInvariant();
        }

        public string BaseDir()
        {
            return Path.GetDirectoryName(MsBuildProject);
        }

        public List<string> FilesInBaseDir()
        {
            var result = new List<string>();
            var baseDir = BaseDir();

            foreach (string file in Files)
            {
                // FIXME This test is not sufficient...
                if (file.StartsWith(baseDir + Path.DirectorySeparatorChar))
                {
                    result.Add(file);
                }
            }

            return result;
        }
    }
}
