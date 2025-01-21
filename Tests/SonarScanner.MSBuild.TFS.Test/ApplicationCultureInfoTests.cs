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
using System.Globalization;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarScanner.MSBuild.Common;

namespace SonarScanner.MSBuild.TFS.Tests;

[TestClass]
[DoNotParallelize]
public class ApplicationCultureInfoTests
{
    [TestMethod]
    public void ApplicationCultureInfoSetAndResetCultureWithUsing()
    {
        var previous = CultureInfo.DefaultThreadCurrentCulture;
        var enUs = CultureInfo.GetCultureInfo("en-US");
        var deDe = CultureInfo.GetCultureInfo("de-DE");
        CultureInfo.DefaultThreadCurrentCulture = deDe;
        try
        {
            CultureInfo.DefaultThreadCurrentCulture.Should().Be(deDe);
            using (new ApplicationCultureInfo(enUs))
            {
                CultureInfo.DefaultThreadCurrentCulture.Should().Be(enUs);
            }
            CultureInfo.DefaultThreadCurrentCulture.Should().Be(deDe);
        }
        finally
        {
            CultureInfo.DefaultThreadCurrentCulture = previous;
        }
    }

    [TestMethod]
    public void ApplicationCultureInfoSetAndResetCultureManual()
    {
        var previous = CultureInfo.DefaultThreadCurrentCulture;
        var enUs = CultureInfo.GetCultureInfo("en-US");
        var deDe = CultureInfo.GetCultureInfo("de-DE");
        CultureInfo.DefaultThreadCurrentCulture = deDe;
        try
        {
            CultureInfo.DefaultThreadCurrentCulture.Should().Be(deDe);
            var sut = new ApplicationCultureInfo(enUs);
            CultureInfo.DefaultThreadCurrentCulture.Should().Be(enUs);
            sut.Dispose();
            CultureInfo.DefaultThreadCurrentCulture.Should().Be(deDe);
        }
        finally
        {
            CultureInfo.DefaultThreadCurrentCulture = previous;
        }
    }

    [TestMethod]
    [ExpectedException(typeof(InvalidOperationException))]
    public void ApplicationCultureInfoSetAndResetCultureOnFailure()
    {
        var previous = CultureInfo.DefaultThreadCurrentCulture;
        var enUs = CultureInfo.GetCultureInfo("en-US");
        var deDe = CultureInfo.GetCultureInfo("de-DE");
        CultureInfo.DefaultThreadCurrentCulture = deDe;
        try
        {
            CultureInfo.DefaultThreadCurrentCulture.Should().Be(deDe);
            using (new ApplicationCultureInfo(enUs))
            {
                CultureInfo.DefaultThreadCurrentCulture.Should().Be(enUs);
                throw new InvalidOperationException();
            }
        }
        finally
        {
            CultureInfo.DefaultThreadCurrentCulture.Should().Be(deDe);
            CultureInfo.DefaultThreadCurrentCulture = previous;
        }
    }
}
