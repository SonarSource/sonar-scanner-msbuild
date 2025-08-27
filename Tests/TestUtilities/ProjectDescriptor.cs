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
/// Describes the expected contents of a single project
/// </summary>
/// <remarks>Used to dynamically create folders and files for tests,
/// and to check actual results against</remarks>
public class ProjectDescriptor
{
    private const string CompilerInputItemGroup = "Compile";
    private const string ContentItemGroup = "Content";

    /// <summary>
    /// Data class to describe a single file in a project.
    /// </summary>
    public class FileInProject
    {
        public FileInProject(string itemGroup, string filePath, bool shouldAnalyse)
        {
            ItemGroup = itemGroup;
            FilePath = filePath;
            ShouldBeAnalysed = shouldAnalyse;
        }

        /// <summary>
        /// The path to the file
        /// </summary>
        public string FilePath { get; set; }

        /// <summary>
        /// The ItemGroup to which the file belongs
        /// </summary>
        public string ItemGroup { get; set; }

        /// <summary>
        /// Whether the file should be include in analysis or not
        /// </summary>
        public bool ShouldBeAnalysed { get; set; }
    }

    public IList<FileInProject> Files { get; set; }

    public ProjectDescriptor()
    {
        AnalysisResults = new List<AnalysisResult>();
        Files = new List<FileInProject>();

        // set default encoding
        Encoding = Encoding.UTF8;
    }

    #region Public properties

    public string ProjectLanguage { get; set; }

    public Guid ProjectGuid { get; set; }

    public IList<string> ManagedSourceFiles
    {
        get
        {
            return Files.Where(f => f.ItemGroup == CompilerInputItemGroup).Select(f => f.FilePath).ToList();
        }
    }

    public bool IsTestProject { get; set; }

    public bool IsExcluded { get; set; }

    public Encoding Encoding { get; set; }

    public List<AnalysisResult> AnalysisResults { get; private set; }

    /// <summary>
    /// The full path to the parent directory
    /// </summary>
    public string ParentDirectoryPath { get; set; }

    /// <summary>
    /// The name of the folder in which the project exists
    /// </summary>
    public string ProjectFolderName { get; set; }

    /// <summary>
    /// The name of the project file
    /// </summary>
    public string ProjectFileName { get; set; }

    public string FullDirectoryPath
    {
        get { return Path.Combine(ParentDirectoryPath, ProjectFolderName); }
    }

    public string FullFilePath
    {
        get { return Path.Combine(FullDirectoryPath, ProjectFileName); }
    }

    /// <summary>
    /// The user-friendly name for the project
    /// </summary>
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
            return Files.Where(f => f.ShouldBeAnalysed).Select(f => f.FilePath);
        }
    }

    /// <summary>
    /// List of files that should not be analyzed
    /// </summary>
    public IEnumerable<string> FilesNotToAnalyse
    {
        get
        {
            return Files.Where(f => !f.ShouldBeAnalysed).Select(f => f.FilePath);
        }
    }

    public bool IsVbProject
    {
        get
        {
            return ProjectLanguages.IsVbProject(ProjectLanguage);
        }
    }

    #endregion Public properties

    #region Public methods

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

    #endregion Public methods
}
