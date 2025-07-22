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

using SonarScanner.MSBuild.Common.RegularExpressions;

namespace SonarScanner.MSBuild.Common.Test.RegularExpressions;

[TestClass]
public class WildcardPatternMatcherTest
{
    /// <summary>
    /// Based on https://github.com/SonarSource/sonar-plugin-api/blob/master/plugin-api/src/test/java/org/sonar/api/utils/WildcardPatternTest.java.
    /// </summary>
    [TestCategory(TestCategories.NoLinux)]
    [TestCategory(TestCategories.NoMacOS)]
    [DataTestMethod]
    [DataRow("Foo", "Foo", true)]
    [DataRow("foo", "FOO", false)]
    [DataRow("Foo", "Foot", false)]
    [DataRow("Foo", "Bar", false)]
    [DataRow("org/T?st.cs", "org\\Test.cs", true)]
    [DataRow("org/T?st.cs", "org\\Tost.cs", true)]
    [DataRow("org/T?st.cs", "org\\Teeest.cs", false)]
    [DataRow("org/*Test.cs", "org\\SignInTest.cs", true)]
    [DataRow("org/*Test.cs", "org\\FooTestContainer.cs", false)]
    [DataRow("org/*Test?.cs", "org\\FooTests.cs", true)]
    [DataRow("org/*Test?.cs", "org\\FooTest.cs", false)]
    [DataRow("org/*Test?.cs", "org\\FooTestContainer.cs", false)]
    [DataRow("org/*Test*.cs", "org\\FooTests.cs", true)]
    [DataRow("org/*Test*.cs", "org\\FooTest.cs", true)]
    [DataRow("org/*Test*.cs", "org\\FooTestContainer.cs", true)]
    [DataRow("org/*.cs", "org\\Foo.cs", true)]
    [DataRow("org/*.cs", "org\\Bar.cs", true)]
    [DataRow("org/**", "org\\Foo.cs", true)]
    [DataRow("org/**", "org\\foo/bar.jsp", true)]
    [DataRow("org/**/Test.cs", "org\\Test.cs", true)]
    [DataRow("org/**/Test.cs", "org\\foo\\Test.cs", true)]
    [DataRow("org/**/Test.cs", "org\\foo\\bar\\Test.cs", true)]
    [DataRow("org/**/*.cs", "org\\Foo.cs", true)]
    [DataRow("org/**/*.cs", "org\\foo\\Bar.cs", true)]
    [DataRow("org/**/*.cs", "org\\foo\\bar\\Baz.cs", true)]
    [DataRow("org/**/**.cs", "org\\foo\\bar\\Baz.cs", true)]
    [DataRow("o?/**/*.cs", "org\\test.cs", false)]
    [DataRow("o?/**/*.cs", "o\\test.cs", false)]
    [DataRow("o?/**/*.cs", "og\\test.cs", true)]
    [DataRow("o?/**/*.cs", "og\\foo\\bar\\test.c", false)]
    [DataRow("o?/**/*.cs", "og\\foo\\bar\\test.cs", true)]
    [DataRow("org/sonar/**", "org\\sonar\\commons\\Foo", true)]
    [DataRow("org/sonar/**", "org\\sonar\\Foo.cs", true)]
    [DataRow("xxx/org/sonar/**", "org\\sonar/Foo", false)]
    [DataRow("org/sonar/**/**", "org\\sonar\\commons\\Foo", true)]
    [DataRow("org/sonar/**/**", "org\\sonar\\commons\\sub\\Foo.cs", true)]
    [DataRow("org/sonar/**/Foo", "org\\sonar\\commons\\sub\\Foo", true)]
    [DataRow("org/sonar/**/Foo", "org\\sonar\\Foo", true)]
    [DataRow("*/foo/*", "org\\foo\\Bar", true)]
    [DataRow("*/foo/*", "foo\\Bar", false)]
    [DataRow("*/foo/*", "foo", false)]
    [DataRow("*/foo/*", "org\\foo\\bar\\Hello", false)]
    [DataRow("foo/*/*.cs", "foo\\Test\\Test.cs", true)]
    [DataRow("foo/*/*.cs", "foo\\Test.cs", false)]
    [DataRow("foo/*/*.cs", "foo\\bar\\baz\\Test.cs", false)]
    [DataRow("hell?", "hell", false)]
    [DataRow("hell?", "hello", true)]
    [DataRow("hell?", "helloworld", false)]
    [DataRow("**/Reader", "java\\io\\Reader", true)]
    [DataRow("**/Reader", "org\\sonar\\channel\\CodeReader", false)]
    [DataRow("**", "java\\io\\Reader", true)]
    [DataRow("**/app/**", "com\\app\\Utils", true)]
    [DataRow("**/app/**", "com\\application\\MyService", false)]
    [DataRow("**/*$*", "foo\\bar", false)]
    [DataRow("**/*$*", "foo\\bar$baz", true)]
    [DataRow("a+", "aa", false)]
    [DataRow("a+", "a+", true)]
    [DataRow("[ab]", "a", false)]
    [DataRow("[ab]", "[ab]", true)]
    [DataRow("/foo", "foo", true)]
    [DataRow("\\n", "\n", false)]
    [DataRow("foo\\bar", "foo\\bar", true)]
    [DataRow("\\foo", "foo", true)]
    [DataRow("foo/bar", "foo\\bar", true)]
    [DataRow("foo\\bar/baz", "foo\\bar\\baz", true)]
    public void IsMatch_MatchesPatternsAsExpected_WindowsPaths(string pattern, string input, bool expectedResult) =>
        WildcardPatternMatcher.IsMatch(pattern, input, false).Should().Be(expectedResult);

    [TestCategory(TestCategories.NoWindows)]
    [DataTestMethod]
    [DataRow("Foo", "Foo", true)]
    [DataRow("foo", "FOO", false)]
    [DataRow("Foo", "Foot", false)]
    [DataRow("Foo", "Bar", false)]
    [DataRow("org/T?st.cs", "org/Test.cs", true)]
    [DataRow("org/T?st.cs", "org/Tost.cs", true)]
    [DataRow("org/T?st.cs", "org/Teeest.cs", false)]
    [DataRow("org/*Test.cs", "org/SignInTest.cs", true)]
    [DataRow("org/*Test.cs", "org/FooTestContainer.cs", false)]
    [DataRow("org/*Test?.cs", "org/FooTests.cs", true)]
    [DataRow("org/*Test?.cs", "org/FooTest.cs", false)]
    [DataRow("org/*Test?.cs", "org/FooTestContainer.cs", false)]
    [DataRow("org/*Test*.cs", "org/FooTests.cs", true)]
    [DataRow("org/*Test*.cs", "org/FooTest.cs", true)]
    [DataRow("org/*Test*.cs", "org/FooTestContainer.cs", true)]
    [DataRow("org/*.cs", "org/Foo.cs", true)]
    [DataRow("org/*.cs", "org/Bar.cs", true)]
    [DataRow("org/**", "org/Foo.cs", true)]
    [DataRow("org/**", "org/foo/bar.jsp", true)]
    [DataRow("org/**/Test.cs", "org/Test.cs", true)]
    [DataRow("org/**/Test.cs", "org/foo/Test.cs", true)]
    [DataRow("org/**/Test.cs", "org/foo/bar/Test.cs", true)]
    [DataRow("org/**/*.cs", "org/Foo.cs", true)]
    [DataRow("org/**/*.cs", "org/foo/Bar.cs", true)]
    [DataRow("org/**/*.cs", "org/foo/bar/Baz.cs", true)]
    [DataRow("org/**/**.cs", "org/foo/bar/Baz.cs", true)]
    [DataRow("o?/**/*.cs", "org/test.cs", false)]
    [DataRow("o?/**/*.cs", "o/test.cs", false)]
    [DataRow("o?/**/*.cs", "og/test.cs", true)]
    [DataRow("o?/**/*.cs", "og/foo/bar/test.cs", true)]
    [DataRow("o?/**/*.cs", "og/foo/bar/test.c", false)]
    [DataRow("org/sonar/**", "org/sonar/commons/Foo", true)]
    [DataRow("org/sonar/**", "org/sonar/Foo.cs", true)]
    [DataRow("xxx/org/sonar/**", "org/sonar/Foo", false)]
    [DataRow("org/sonar/**/**", "org/sonar/commons/Foo", true)]
    [DataRow("org/sonar/**/**", "org/sonar/commons/sub/Foo.cs", true)]
    [DataRow("org/sonar/**/Foo", "org/sonar/commons/sub/Foo", true)]
    [DataRow("org/sonar/**/Foo", "org/sonar/Foo", true)]
    [DataRow("*/foo/*", "org/foo/Bar", true)]
    [DataRow("*/foo/*", "foo/Bar", false)]
    [DataRow("*/foo/*", "foo", false)]
    [DataRow("*/foo/*", "org/foo/bar/Hello", false)]
    [DataRow("foo/*/*.cs", "foo/Test/Test.cs", true)]
    [DataRow("foo/*/*.cs", "foo/Test.cs", false)]
    [DataRow("foo/*/*.cs", "foo/bar/baz/Test.cs", false)]
    [DataRow("hell?", "hell", false)]
    [DataRow("hell?", "hello", true)]
    [DataRow("hell?", "helloworld", false)]
    [DataRow("**/Reader", "java/io/Reader", true)]
    [DataRow("**/Reader", "org/sonar/channel/CodeReader", false)]
    [DataRow("**", "java/io/Reader", true)]
    [DataRow("**/app/**", "com/app/Utils", true)]
    [DataRow("**/app/**", "com/application/MyService", false)]
    [DataRow("**/*$*", "foo/bar", false)]
    [DataRow("**/*$*", "foo/bar$baz", true)]
    [DataRow("a+", "aa", false)]
    [DataRow("a+", "a+", true)]
    [DataRow("[ab]", "a", false)]
    [DataRow("[ab]", "[ab]", true)]
    [DataRow("/foo", "foo", true)]
    [DataRow("\\n", "\n", false)]
    [DataRow("foo\\bar", "foo/bar", true)]
    [DataRow("\\foo", "foo", true)]
    [DataRow("foo/bar", "foo/bar", true)]
    [DataRow("foo\\bar/baz", "foo/bar/baz", true)]
    public void IsMatch_MatchesPatternsAsExpected_UnixPaths(string pattern, string input, bool expectedResult) =>
        WildcardPatternMatcher.IsMatch(pattern, input, false).Should().Be(expectedResult);

    [DataTestMethod]
    [DataRow("")]
    [DataRow("  ")]
    [DataRow("/")]
    [DataRow("\\")]
    public void IsMatch_InvalidPattern_ReturnsFalse(string pattern) =>
        WildcardPatternMatcher.IsMatch(pattern, "foo", false).Should().BeFalse();

    [DataTestMethod]
    [DataRow(null, "foo")]
    [DataRow("foo", null)]
    [DataRow("", "foo")]
    [DataRow("foo", "")]
    public void IsMatch_InputParametersAreNull_DoesNotThrow(string pattern, string input) =>
        WildcardPatternMatcher.IsMatch(pattern, input, false).Should().BeFalse();
}
