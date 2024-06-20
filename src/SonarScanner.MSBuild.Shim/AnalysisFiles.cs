using System.Collections.Generic;
using System.IO;

namespace SonarScanner.MSBuild.Shim;

public sealed class AnalysisFiles(ICollection<FileInfo> sources, ICollection<FileInfo> tests)
{
    public ICollection<FileInfo> Sources { get; } = sources;

    public ICollection<FileInfo> Tests { get; } = tests;
}
