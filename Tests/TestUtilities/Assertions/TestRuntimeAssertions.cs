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

using FluentAssertions.Primitives;

namespace TestUtilities.Assertions;

public static class TestRuntimeExtensions
{
    public static TestRuntimeAssertions Should(this TestRuntime subject) =>
        new(subject);
}

public class TestRuntimeAssertions : ReferenceTypeAssertions<TestRuntime, TestRuntimeAssertions>
{
    protected override string Identifier { get; } = nameof(TestRuntime);

    public TestRuntimeAssertions(TestRuntime subject) : base(subject) { }

    [CustomAssertion]
    public AndConstraint<TestRuntimeAssertions> HaveErrorsLogged(int expectedCount, string because = "", params string[] becauseArgs)
    {
        Subject.Logger.Should().HaveErrors(expectedCount, because, becauseArgs);
        return new(this);
    }

    [CustomAssertion]
    public AndConstraint<TestRuntimeAssertions> HaveWarningsLogged(int expectedCount, string because = "", params string[] becauseArgs)
    {
        Subject.Logger.Should().HaveWarnings(expectedCount, because, becauseArgs);
        return new(this);
    }

    [CustomAssertion]
    public AndConstraint<TestRuntimeAssertions> HaveInfosLogged(int expectedCount, string because = "", params string[] becauseArgs)
    {
        Subject.Logger.Should().HaveInfos(expectedCount, because, becauseArgs);
        return new(this);
    }

    [CustomAssertion]
    public AndConstraint<TestRuntimeAssertions> HaveErrorsLogged(params string[] expectedMessages)
    {
        Subject.Logger.Should().HaveErrors(expectedMessages);
        return new(this);
    }

    [CustomAssertion]
    public AndConstraint<TestRuntimeAssertions> HaveWarningsLogged(params string[] expectedMessages)
    {
        Subject.Logger.Should().HaveWarnings(expectedMessages);
        return new(this);
    }

    [CustomAssertion]
    public AndConstraint<TestRuntimeAssertions> HaveDebugsLogged(params string[] expectedMessages)
    {
        Subject.Logger.Should().HaveDebugs(expectedMessages);
        return new(this);
    }

    [CustomAssertion]
    public AndConstraint<TestRuntimeAssertions> HaveInfosLogged(params string[] expectedMessages)
    {
        Subject.Logger.Should().HaveInfos(expectedMessages);
        return new(this);
    }

    [CustomAssertion]
    public AndConstraint<TestRuntimeAssertions> HaveNoErrorsLogged(string because = "", params string[] becauseArgs)
    {
        Subject.Logger.Should().HaveNoErrors(because, becauseArgs);
        return new(this);
    }

    [CustomAssertion]
    public AndConstraint<TestRuntimeAssertions> HaveNoWarningsLogged(string because = "", params string[] becauseArgs)
    {
        Subject.Logger.Should().HaveNoWarnings(because, becauseArgs);
        return new(this);
    }

    [CustomAssertion]
    public AndConstraint<TestRuntimeAssertions> NotHaveErrorLogged(string notExpectedMessage, string because = "", params string[] becauseArgs)
    {
        Subject.Logger.Should().NotHaveError(notExpectedMessage, because, becauseArgs);
        return new(this);
    }

    [CustomAssertion]
    public AndConstraint<TestRuntimeAssertions> NotHaveDebugLogged(string notExpectedMessage, string because = "", params string[] becauseArgs)
    {
        Subject.Logger.Should().NotHaveDebug(notExpectedMessage, because, becauseArgs);
        return new(this);
    }

    [CustomAssertion]
    public AndConstraint<TestRuntimeAssertions> NotHaveInfoLogged(string notExpectedMessage, string because = "", params string[] becauseArgs)
    {
        Subject.Logger.Should().NotHaveInfo(notExpectedMessage, because, becauseArgs);
        return new(this);
    }

    [CustomAssertion]
    public AndConstraint<TestRuntimeAssertions> HaveSingleErrorLogged(string expectedMessage, string because = "", params string[] becauseArgs)
    {
        Subject.Logger.Should().HaveSingleError(expectedMessage, because, becauseArgs);
        return new(this);
    }

    [CustomAssertion]
    public AndConstraint<TestRuntimeAssertions> HaveSingleWarningLogged(string expectedMessage, string because = "", params string[] becauseArgs)
    {
        Subject.Logger.Should().HaveSingleWarning(expectedMessage, because, becauseArgs);
        return new(this);
    }

    [CustomAssertion]
    public AndConstraint<TestRuntimeAssertions> HaveSingleDebugLogged(string expectedMessage, string because = "", params string[] becauseArgs)
    {
        Subject.Logger.Should().HaveSingleDebug(expectedMessage, because, becauseArgs);
        return new(this);
    }

    [CustomAssertion]
    public AndConstraint<TestRuntimeAssertions> HaveSingleInfoLogged(string expectedMessage, string because = "", params string[] becauseArgs)
    {
        Subject.Logger.Should().HaveSingleInfo(expectedMessage, because, becauseArgs);
        return new(this);
    }
}
