/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2025 SonarSource SA
 * mailto: info AT sonarsource DOT com
 *
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with this program; if not, write to the Free Software Foundation,
 * Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.
 */

namespace TestUtilities;

/// <summary>
/// Describes the expected contents of a single project.
/// </summary>
/// <remarks>Used to dynamically create folders and files for tests, and to check actual results against.</remarks>
public class ProjectDescriptor
{
    private const string CompilerInputItemGroup = "Compile";
    private const string ContentItemGroup = "Content";

    public IList<FileInProject> Files { get; set; }

    public string ProjectLanguage { get; set; }

    public Guid ProjectGuid { get; set; }

    public IList<string> ManagedSourceFiles
    {
        get
        {
            return Files.Where(x => x.ItemGroup == CompilerInputItemGroup).Select(x => x.FilePath).ToList();
        }
    }

    public bool IsTestProject { get; set; }

    public bool IsExcluded { get; set; }

    public Encoding Encoding { get; set; }

    public List<AnalysisResult> AnalysisResults { get; private set; }

    public string ParentDirectoryPath { get; set; }

    public string ProjectFolderName { get; set; }

    public string ProjectFileName { get; set; }

    public string FullDirectoryPath
    {
        get { return Path.Combine(ParentDirectoryPath, ProjectFolderName); }
    }

    public string FullFilePath
    {
        get { return Path.Combine(FullDirectoryPath, ProjectFileName); }
    }

    public string ProjectName
    {
        get
        {
            return Path.GetFileNameWithoutExtension(ProjectFileName);
        }
    }

    public IEnumerable<string> FilesToAnalyse
    {
        get
        {
            return Files.Where(x => x.ShouldBeAnalysed).Select(x => x.FilePath);
        }
    }

    public IEnumerable<string> FilesNotToAnalyse
    {
        get
        {
            return Files.Where(x => !x.ShouldBeAnalysed).Select(x => x.FilePath);
        }
    }

    public bool IsVbProject
    {
        get
        {
            return ProjectLanguages.IsVbProject(ProjectLanguage);
        }
    }

    public ProjectDescriptor()
    {
        AnalysisResults = new List<AnalysisResult>();
        Files = new List<FileInProject>();

        // set default encoding
        Encoding = Encoding.UTF8;
    }

    public ProjectInfo CreateProjectInfo()
    {
        var info = new ProjectInfo()
        {
            FullPath = FullFilePath,
            ProjectLanguage = ProjectLanguage,
            ProjectGuid = ProjectGuid,
            ProjectName = ProjectName,
            ProjectType = IsTestProject ? ProjectType.Test : ProjectType.Product,
            IsExcluded = IsExcluded,
            Encoding = Encoding.WebName,
            AnalysisResults = new List<AnalysisResult>(AnalysisResults)
        };

        return info;
    }

    public void AddContentFile(string filePath, bool shouldAnalyse)
    {
        Files.Add(new FileInProject(ProjectDescriptor.ContentItemGroup, filePath, shouldAnalyse));
    }

    public void AddCompileInputFile(string filePath, bool shouldAnalyse)
    {
        Files.Add(new FileInProject(ProjectDescriptor.CompilerInputItemGroup, filePath, shouldAnalyse));
    }

    public class FileInProject
    {
        public string FilePath { get; set; }

        public string ItemGroup { get; set; }

        public bool ShouldBeAnalysed { get; set; }

        public FileInProject(string itemGroup, string filePath, bool shouldAnalyse)
        {
            ItemGroup = itemGroup;
            FilePath = filePath;
            ShouldBeAnalysed = shouldAnalyse;
        }
    }
}
