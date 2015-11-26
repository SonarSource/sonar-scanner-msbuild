using SonarQube.Common;

namespace SonarRunner.Shim
{
    public interface IRoslynV1SarifFixer
    {
        bool FixRoslynV1SarifFile(string sarifFilePath, ILogger logger);
    }
}