﻿/*
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

using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarScanner.MSBuild.Common.CommandLine;

namespace SonarScanner.MSBuild.Common.Test;

[TestClass]
public class CommandLineFlagPrefixTests
{ 
    #region Tests

    [TestMethod]
    public void Parser_ShouldReturnPrefixedFlags()
    {
        //arrange
        var args = new string[] { "a:", "alias:" };

        var expectedResult = new string[] { "-a:", "/a:", "-alias:", "/alias:" };

        //act
        var argsPrefixed = CommandLineFlagPrefix.GetPrefixedFlags(args);

        //assert
        CollectionAssert.AreEqual(argsPrefixed, expectedResult);
    }

    #endregion Tests

}
