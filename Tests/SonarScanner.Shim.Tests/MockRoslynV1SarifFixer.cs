/*
 * SonarQube Scanner for MSBuild
 * Copyright (C) 2016-2017 SonarSource SA
 * mailto:info AT sonarsource DOT com
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
 
using SonarQube.Common;

namespace SonarScanner.Shim.Tests
{
    class MockRoslynV1SarifFixer : IRoslynV1SarifFixer
    {

        #region Test Hooks

        public string ReturnVal { get; set; }

        public int CallCount { get; set; }

        public string LastLanguage { get; set; }

        public MockRoslynV1SarifFixer(string returnVal)
        {
            this.ReturnVal = returnVal;
            this.CallCount = 0;
        }

        #endregion

        #region IRoslynV1SarifFixer

        public string LoadAndFixFile(string sarifPath, string language, ILogger logger)
        {
            CallCount++;
            LastLanguage = language;
            return ReturnVal;
        }

        #endregion
    }
}
