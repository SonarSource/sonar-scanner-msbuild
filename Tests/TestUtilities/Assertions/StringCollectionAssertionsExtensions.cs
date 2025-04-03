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

using FluentAssertions.Collections;
using FluentAssertions.Execution;

namespace TestUtilities.Assertions;

public static class StringCollectionAssertionsExtensions
{
    public static void ContainIgnoringLineEndings(this StringCollectionAssertions assertions, string expected)
    {
        var normalizedExpected = expected.NormalizeLineEndings();
        var normalizedCollection = assertions.Subject.Select(StringExtensions.NormalizeLineEndings);

        Execute.Assertion
            .ForCondition(normalizedCollection.Contains(normalizedExpected))
            .FailWith("Expected {context:collection} to contain {0} ignoring line endings, but it did not.", expected);
    }
}
