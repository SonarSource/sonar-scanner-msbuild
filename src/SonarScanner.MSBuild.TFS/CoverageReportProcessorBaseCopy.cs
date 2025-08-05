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

namespace SonarScanner.MSBuild.TFS;

public abstract class CoverageReportProcessorBaseCopy : ICoverageReportProcessor
{
    private const string XmlReportFileExtension = "coveragexml";
    private readonly ICoverageReportConverter converter;

    private AnalysisConfig config;
    private IBuildSettings settings;
    private string propertiesFilePath;

    private bool successfullyInitialized;

    protected ILogger Logger { get; }

    protected CoverageReportProcessorBaseCopy(ICoverageReportConverter converter, ILogger logger)
    {
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.converter = converter ?? throw new ArgumentNullException(nameof(converter));
    }

    public bool Initialise(AnalysisConfig config, IBuildSettings settings, string propertiesFilePath)
    {
        this.config = config ?? throw new ArgumentNullException(nameof(config));
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        this.propertiesFilePath = propertiesFilePath ?? throw new ArgumentNullException(nameof(propertiesFilePath));
        successfullyInitialized = true;

        return successfullyInitialized;
    }

    public bool ProcessCoverageReports(ILogger logger)
    {
        if (!successfullyInitialized)
        {
            throw new InvalidOperationException(Resources.EX_CoverageReportProcessorNotInitialized);
        }

        if (config.GetSettingOrDefault(SonarProperties.VsTestReportsPaths, true, null, logger) != null)
        {
            Logger.LogInfo(Resources.TRX_DIAG_SkippingCoverageCheckPropertyProvided);
        }
        else
        {
            // Fetch all of the report URLs
            Logger.LogInfo(Resources.PROC_DIAG_FetchingCoverageReportInfoFromServer);

            if (TryGetTrxFiles(settings, out var trxPaths) &&
                trxPaths.Any())
            {
                WriteProperty(propertiesFilePath, SonarProperties.VsTestReportsPaths, trxPaths.ToArray());
            }
        }

        var success = TryGetVsCoverageFiles(config, settings, out var vsCoverageFilePaths);
        if (success &&
            vsCoverageFilePaths.Any() &&
            TryConvertCoverageReports(vsCoverageFilePaths, out var coverageReportPaths) &&
            coverageReportPaths.Any() &&
            config.GetSettingOrDefault(SonarProperties.VsCoverageXmlReportsPaths, true, null, logger) == null)
        {
            WriteProperty(propertiesFilePath, SonarProperties.VsCoverageXmlReportsPaths, coverageReportPaths.ToArray());
        }

        return success;
    }

    protected abstract bool TryGetVsCoverageFiles(AnalysisConfig config, IBuildSettings settings, out IEnumerable<string> binaryFilePaths);

    protected abstract bool TryGetTrxFiles(IBuildSettings settings, out IEnumerable<string> trxFilePaths);

    private bool TryConvertCoverageReports(IEnumerable<string> vsCoverageFilePaths, out IEnumerable<string> vsCoverageXmlPaths)
    {
        var xmlFileNames = new List<string>();

        foreach (var vsCoverageFilePath in vsCoverageFilePaths)
        {
            var xmlFilePath = Path.ChangeExtension(vsCoverageFilePath, XmlReportFileExtension);
            if (File.Exists(xmlFilePath))
            {
                Logger.LogInfo(string.Format(Resources.COVXML_DIAG_FileAlreadyExist_NoConversionAttempted, vsCoverageFilePath));
            }
            else
            {
                if (!converter.ConvertToXml(vsCoverageFilePath, xmlFilePath))
                {
                    vsCoverageXmlPaths = Enumerable.Empty<string>();
                    return false;
                }
            }

            xmlFileNames.Add(xmlFilePath);
        }

        vsCoverageXmlPaths = xmlFileNames;
        return true;
    }

    private static void WriteProperty(string propertiesFilePath, string property, string[] paths) =>
        File.AppendAllText(propertiesFilePath, $"{Environment.NewLine}{property}={string.Join(",", paths.Select(c => c.Replace(@"\", @"\\")))}");
}
