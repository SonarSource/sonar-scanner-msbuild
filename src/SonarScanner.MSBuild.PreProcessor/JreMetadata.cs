namespace SonarScanner.MSBuild.PreProcessor;

public sealed class JreMetadata(string Id, string Filename, string JavaPath, string DownloadUrl, string Sha256)
{
    public string Id { get; } = Id;                     // Optional, only exists for SonarQube
    public string Filename { get; } = Filename;
    public string Sha256 { get; } = Sha256;
    public string JavaPath { get; } = JavaPath;
    public string DownloadUrl { get; } = DownloadUrl;   // Optional, only exists for SonarCloud
}
