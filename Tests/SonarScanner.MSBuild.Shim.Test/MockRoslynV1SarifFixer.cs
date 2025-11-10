/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2025 SonarSource Sàrl
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

namespace SonarScanner.MSBuild.Shim.Test;

internal class MockRoslynV1SarifFixer : RoslynV1SarifFixer
{
    public string ReturnVal { get; }
    public string LastLanguage { get; private set; }
    public int CallCount { get; private set; }

    /// <param name="returnVal">Provide null to return the original input value with ".fixed.mock.json" suffix</param>
    public MockRoslynV1SarifFixer(string returnVal) : base(Substitute.For<ILogger>()) =>
        ReturnVal = returnVal;

    public override string LoadAndFixFile(string sarifFilePath, string language)
    {
        CallCount++;
        LastLanguage = language;
        return ReturnVal ?? sarifFilePath + ".fixed.mock.json";
    }
}
