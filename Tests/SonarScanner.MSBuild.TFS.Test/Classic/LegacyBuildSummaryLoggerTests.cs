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

using SonarScanner.MSBuild.TFS.Classic.XamlBuild;

namespace SonarScanner.MSBuild.TFS.Test;

[TestClass]
public class LegacyBuildSummaryLoggerTests
{
    [TestMethod]
    public void Ctor_TfsUriIsNull_Throws()
    {
        // Arrange
        Action action = () => new LegacyBuildSummaryLogger(null, "http://builduri");

        // Act & Assert
        action.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("tfsUri");
    }

    [TestMethod]
    public void Ctor_BuildUriIsNull_Throws()
    {
        // Arrange
        Action action = () => new LegacyBuildSummaryLogger("http://tfsUri", null);

        // Act & Assert
        action.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("buildUri");
    }

    [TestMethod]
    public void WriteMessage_MessageIsNull_Throws()
    {
        // Arrange
        var testSubject = new LegacyBuildSummaryLogger("http://tfsUri", "http://builduri");
        Action action = () => testSubject.WriteMessage(null);

        // Act & Assert
        action.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("message");
    }
}
