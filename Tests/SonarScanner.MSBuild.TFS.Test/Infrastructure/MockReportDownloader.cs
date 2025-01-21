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

using System;
using System.Collections.Generic;
using System.IO;
using FluentAssertions;
using SonarScanner.MSBuild.TFS.Classic.XamlBuild;

namespace SonarScanner.MSBuild.TFS.Tests.Infrastructure;

internal class MockReportDownloader : ICoverageReportDownloader
{
    private int callCount;
    private readonly IList<string> requestedUrls = new List<string>();
    private readonly IList<string> targetFileNames  = new List<string>();

    #region Test helpers

    public bool CreateFileOnDownloadRequest { get; set; }

    #endregion Test helpers

    #region Assertions

    public void AssertExpectedDownloads(int expected)
    {
        callCount.Should().Be(expected, "DownloadReport called an unexpected number of times");
    }

    public void AssertDownloadNotCalled()
    {
        callCount.Should().Be(0, "Not expecting DownloadReport to have been called");
    }

    public void AssertExpectedUrlsRequested(params string[] urls)
    {
        requestedUrls.Should().BeEquivalentTo(urls, "Unexpected urls requested");
    }

    public void AssertExpectedTargetFileNamesSupplied(params string[] urls)
    {
        targetFileNames.Should().BeEquivalentTo(urls, "Unexpected target files names supplied");
    }

    #endregion Assertions

    #region ICoverageReportDownloader interface

    bool ICoverageReportDownloader.DownloadReport(string tfsUri, string reportUrl, string newFullFileName, TimeSpan httpTimeout)
    {
        callCount++;
        requestedUrls.Add(reportUrl);
        targetFileNames.Add(newFullFileName);

        if (CreateFileOnDownloadRequest)
        {
            File.WriteAllText(newFullFileName, string.Empty);
        }

        return CreateFileOnDownloadRequest;
    }

    #endregion ICoverageReportDownloader interface
}
