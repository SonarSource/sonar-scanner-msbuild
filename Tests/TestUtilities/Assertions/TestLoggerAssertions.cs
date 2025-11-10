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

using FluentAssertions.Execution;
using FluentAssertions.Primitives;

namespace TestUtilities.Assertions;

public static class TestLoggerExtensions
{
    public static TestLoggerAssertions Should(this TestLogger subject) =>
        new(subject);
}

public class TestLoggerAssertions : ReferenceTypeAssertions<TestLogger, TestLoggerAssertions>
{
    protected override string Identifier { get; } = nameof(TestLogger);

    public TestLoggerAssertions(TestLogger subject) : base(subject) { }

    [CustomAssertion]
    public AndWhichConstraint<TestLoggerAssertions, List<string>> HaveErrors(int expectedCount, string because = "", params string[] becauseArgs)
    {
        Execute.Assertion
            .BecauseOf(because, becauseArgs)
            .ForCondition(Subject.Errors.Count == expectedCount)
            .FailWith($"Expected {expectedCount} errors to be logged, but found {Subject.Errors.Count}.");
        return new(this, Subject.Errors);
    }

    [CustomAssertion]
    public AndWhichConstraint<TestLoggerAssertions, List<string>> HaveWarnings(int expectedCount, string because = "", params string[] becauseArgs)
    {
        Execute.Assertion
            .BecauseOf(because, becauseArgs)
            .ForCondition(Subject.Warnings.Count == expectedCount)
            .FailWith($"Expected {expectedCount} warnings to be logged, but found {Subject.Warnings.Count}.");
        return new(this, Subject.Warnings);
    }

    [CustomAssertion]
    public AndWhichConstraint<TestLoggerAssertions, List<string>> HaveDebugs(int expectedCount, string because = "", params string[] becauseArgs)
    {
        Execute.Assertion
            .BecauseOf(because, becauseArgs)
            .ForCondition(Subject.DebugMessages.Count == expectedCount)
            .FailWith($"Expected {expectedCount} DEBUG messages to be logged, but found {Subject.DebugMessages.Count}.");
        return new(this, Subject.DebugMessages);
    }

    [CustomAssertion]
    public AndWhichConstraint<TestLoggerAssertions, List<string>> HaveInfos(int expectedCount, string because = "", params string[] becauseArgs)
    {
        Execute.Assertion
            .BecauseOf(because, becauseArgs)
            .ForCondition(Subject.InfoMessages.Count == expectedCount)
            .FailWith($"Expected {expectedCount} INFO messages to be logged, but found {Subject.InfoMessages.Count}.");
        return new(this, Subject.InfoMessages);
    }

    [CustomAssertion]
    public AndConstraint<TestLoggerAssertions> HaveErrors(params string[] expectedMessages)
    {
        if (expectedMessages.Length == 0)
        {
            Execute.Assertion
                .ForCondition(Subject.Errors.Count > 0)
                .FailWith("Expected at least one error to be logged, but found none.");
        }
        else
        {
            Execute.Assertion
                .ForCondition(expectedMessages.All(x => Subject.Errors.Contains(x.ToUnixLineEndings())))
                .FailWith($"""
                    Expected the following errors to be logged:
                    {ListOfQuotedStrings(expectedMessages.Except(Subject.Errors))}
                    but could not find them in
                    {ListOfQuotedStrings(Subject.Errors)}
                    """);
        }
        return new(this);
    }

    [CustomAssertion]
    public AndConstraint<TestLoggerAssertions> HaveWarnings(params string[] expectedMessages)
    {
        if (expectedMessages.Length == 0)
        {
            Execute.Assertion
                .ForCondition(Subject.Warnings.Count > 0)
                .FailWith("Expected at least one warning to be logged, but found none.");
        }
        else
        {
            Execute.Assertion
                .ForCondition(expectedMessages.All(x => Subject.Warnings.Contains(x.ToUnixLineEndings())))
                .FailWith($"""
                    Expected the following warnings to be logged:
                    {ListOfQuotedStrings(expectedMessages.Except(Subject.Warnings))}
                    but could not find them in
                    {ListOfQuotedStrings(Subject.Warnings)}
                    """);
        }
        return new(this);
    }

    [CustomAssertion]
    public AndConstraint<TestLoggerAssertions> HaveDebugs(params string[] expectedMessages)
    {
        if (expectedMessages.Length == 0)
        {
            Execute.Assertion
                .ForCondition(Subject.DebugMessages.Count > 0)
                .FailWith("Expected at least one DEBUG message to be logged, but found none.");
        }
        else
        {
            Execute.Assertion
                .ForCondition(expectedMessages.All(x => Subject.DebugMessages.Contains(x.ToUnixLineEndings())))
                .FailWith($"""
                    Expected the following DEBUG messages to be logged:
                    {ListOfQuotedStrings(expectedMessages.Except(Subject.DebugMessages))}
                    but could not find them in
                    {ListOfQuotedStrings(Subject.DebugMessages)}
                    """);
        }
        return new(this);
    }

    [CustomAssertion]
    public AndConstraint<TestLoggerAssertions> HaveInfos(params string[] expectedMessages)
    {
        if (expectedMessages.Length == 0)
        {
            Execute.Assertion
                .ForCondition(Subject.InfoMessages.Count > 0)
                .FailWith("Expected at least one INFO message to be logged, but found none.");
        }
        else
        {
            Execute.Assertion
                .ForCondition(expectedMessages.All(x => Subject.InfoMessages.Contains(x.ToUnixLineEndings())))
                .FailWith($"""
                    Expected the following INFO messages to be logged:
                    {ListOfQuotedStrings(expectedMessages.Except(Subject.InfoMessages))}
                    but could not find them in
                    {ListOfQuotedStrings(Subject.InfoMessages)}
                    """);
        }
        return new(this);
    }

    [CustomAssertion]
    public AndConstraint<TestLoggerAssertions> HaveNoErrors(string because = "", params string[] becauseArgs)
    {
        Execute.Assertion
            .BecauseOf(because, becauseArgs)
            .ForCondition(Subject.Errors.Count == 0)
            .FailWith($"Expected no errors to be logged, but found:\n{ListOfQuotedStrings(Subject.Warnings)}");
        return new(this);
    }

    [CustomAssertion]
    public AndConstraint<TestLoggerAssertions> HaveNoWarnings(string because = "", params string[] becauseArgs)
    {
        Execute.Assertion
            .BecauseOf(because, becauseArgs)
            .ForCondition(Subject.Warnings.Count == 0)
            .FailWith($"Expected no warnings to be logged, but found:\n{ListOfQuotedStrings(Subject.Warnings)}");
        return new(this);
    }

    [CustomAssertion]
    public AndConstraint<TestLoggerAssertions> HaveNoDebugs(string because = "", params string[] becauseArgs)
    {
        Execute.Assertion
            .BecauseOf(because, becauseArgs)
            .ForCondition(Subject.DebugMessages.Count == 0)
            .FailWith($"Expected no DEBUG messages to be logged, but found:\n{ListOfQuotedStrings(Subject.DebugMessages)}");
        return new(this);
    }

    [CustomAssertion]
    public AndConstraint<TestLoggerAssertions> HaveNoInfos(string because = "", params string[] becauseArgs)
    {
        Execute.Assertion
            .BecauseOf(because, becauseArgs)
            .ForCondition(Subject.InfoMessages.Count == 0)
            .FailWith($"Expected no INFO messages to be logged, but found:\n{ListOfQuotedStrings(Subject.InfoMessages)}");
        return new(this);
    }

    [CustomAssertion]
    public AndConstraint<TestLoggerAssertions> NotHaveError(string notExpectedMessage, string because = "", params string[] becauseArgs)
    {
        Execute.Assertion
            .BecauseOf(because, becauseArgs)
            .ForCondition(!Subject.Errors.Any(x => notExpectedMessage.ToUnixLineEndings().Equals(x, StringComparison.CurrentCulture)))
            .FailWith($"Expected the error \"{notExpectedMessage}\" to not be logged, but found it in:\n{ListOfQuotedStrings(Subject.Errors)}");
        return new(this);
    }

    [CustomAssertion]
    public AndConstraint<TestLoggerAssertions> NotHaveDebug(string notExpectedMessage, string because = "", params string[] becauseArgs)
    {
        Execute.Assertion
            .BecauseOf(because, becauseArgs)
            .ForCondition(!Subject.DebugMessages.Any(x => notExpectedMessage.ToUnixLineEndings().Equals(x, StringComparison.CurrentCulture)))
            .FailWith($"Expected the DEBUG message \"{notExpectedMessage}\" to not be logged, but found it in:\n{ListOfQuotedStrings(Subject.DebugMessages)}");
        return new(this);
    }

    [CustomAssertion]
    public AndConstraint<TestLoggerAssertions> NotHaveInfo(string notExpectedMessage, string because = "", params string[] becauseArgs)
    {
        Execute.Assertion
            .BecauseOf(because, becauseArgs)
            .ForCondition(!Subject.InfoMessages.Any(x => notExpectedMessage.ToUnixLineEndings().Equals(x, StringComparison.CurrentCulture)))
            .FailWith($"Expected the INFO message \"{notExpectedMessage}\" to not be logged, but found it in:\n{ListOfQuotedStrings(Subject.InfoMessages)}");
        return new(this);
    }

    [CustomAssertion]
    public AndConstraint<TestLoggerAssertions> HaveErrorOnce(string expectedMessage, string because = "", params string[] becauseArgs)
    {
        var matches = Subject.Errors.Count(x => expectedMessage.ToUnixLineEndings().Equals(x, StringComparison.CurrentCulture));
        Execute.Assertion
            .BecauseOf(because, becauseArgs)
            .ForCondition(matches == 1)
            .FailWith(matches == 0
            ? $"Expected the error \"{expectedMessage}\" to be logged, but could not find it in:\n{ListOfQuotedStrings(Subject.Errors)}"
            : $"Expected the error \"{expectedMessage}\" to be logged exactly once, but found it multiple times in:\n{ListOfQuotedStrings(Subject.Errors)}");
        return new(this);
    }

    [CustomAssertion]
    public AndConstraint<TestLoggerAssertions> HaveWarningOnce(string expectedMessage, string because = "", params string[] becauseArgs)
    {
        var matches = Subject.Warnings.Count(x => expectedMessage.ToUnixLineEndings().Equals(x, StringComparison.CurrentCulture));
        Execute.Assertion
            .BecauseOf(because, becauseArgs)
            .ForCondition(matches == 1)
            .FailWith(matches == 0
            ? $"Expected the warning \"{expectedMessage}\" to be logged, but could not find it in:\n{ListOfQuotedStrings(Subject.Warnings)}"
            : $"Expected the warnings \"{expectedMessage}\" to be logged exactly once, but found it multiple times in:\n{ListOfQuotedStrings(Subject.Warnings)}");
        return new(this);
    }

    [CustomAssertion]
    public AndConstraint<TestLoggerAssertions> HaveDebugOnce(string expectedMessage, string because = "", params string[] becauseArgs)
    {
        var matches = Subject.DebugMessages.Count(x => expectedMessage.ToUnixLineEndings().Equals(x, StringComparison.CurrentCulture));
        Execute.Assertion
            .BecauseOf(because, becauseArgs)
            .ForCondition(matches == 1)
            .FailWith(matches == 0
            ? $"Expected the DEBUG message \"{expectedMessage}\" to be logged, but could not find it in:\n{ListOfQuotedStrings(Subject.DebugMessages)}"
            : $"Expected the DEBUG message \"{expectedMessage}\" to be logged exactly once, but found it multiple times in:\n{ListOfQuotedStrings(Subject.DebugMessages)}");
        return new(this);
    }

    [CustomAssertion]
    public AndConstraint<TestLoggerAssertions> HaveInfoOnce(string expectedMessage, string because = "", params string[] becauseArgs)
    {
        var matches = Subject.InfoMessages.Count(x => expectedMessage.ToUnixLineEndings().Equals(x, StringComparison.CurrentCulture));
        Execute.Assertion
            .BecauseOf(because, becauseArgs)
            .ForCondition(matches == 1)
            .FailWith(matches == 0
            ? $"Expected the INFO message \"{expectedMessage}\" to be logged, but could not find it in:\n{ListOfQuotedStrings(Subject.InfoMessages)}"
            : $"Expected the INFO message \"{expectedMessage}\" to be logged exactly once, but found it multiple times in:\n{ListOfQuotedStrings(Subject.InfoMessages)}");
        return new(this);
    }

    private static string ListOfQuotedStrings(IEnumerable<string> strings) =>
        "\"" + string.Join("\"\n\"", strings) + "\"";
}
