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

using FluentAssertions.Execution;
using FluentAssertions.Primitives;

namespace TestUtilities.Assertions;

public static class TestAnalysisWarningsExtensions
{
    public static TestAnalysisWarningsAssertions Should(this TestAnalysisWarnings subject) =>
        new(subject);
}

public class TestAnalysisWarningsAssertions : ReferenceTypeAssertions<TestAnalysisWarnings, TestAnalysisWarningsAssertions>
{
    protected override string Identifier { get; } = nameof(TestAnalysisWarnings);

    public TestAnalysisWarningsAssertions(TestAnalysisWarnings subject) : base(subject) { }

    [CustomAssertion]
    public AndConstraint<TestAnalysisWarningsAssertions> HaveNoMessages(string because = "", params string[] becauseArgs)
    {
        Execute.Assertion
            .BecauseOf(because, becauseArgs)
            .ForCondition(Subject.Messages.Count == 0)
            .FailWith($"Expected no analysis warnings to be logged, but found:\n{ListOfQuotedStrings(Subject.Messages)}");
        return new(this);
    }

    [CustomAssertion]
    public AndConstraint<TestAnalysisWarningsAssertions> HaveMessage(string expected, string because = "", params string[] becauseArgs)
    {
        Execute.Assertion
            .BecauseOf(because, becauseArgs)
            .ForCondition(Subject.Messages.Contains(expected.ToUnixLineEndings()))
            .FailWith($"""
                Expected the following analysis warning to be logged:
                {expected}
                but could not find it in
                {ListOfQuotedStrings(Subject.Messages)}
                """);
        return new(this);
    }

    private static string ListOfQuotedStrings(IEnumerable<string> strings) =>
        "\"" + string.Join("\"\n\"", strings) + "\"";
}
