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

public static class TestTelemetryExtensions
{
    public static TestTelemetryAssertions Should(this TestTelemetry subject) =>
        new(subject);
}

public class TestTelemetryAssertions : ReferenceTypeAssertions<TestTelemetry, TestTelemetryAssertions>
{
    protected override string Identifier { get; } = nameof(TestTelemetry);

    public TestTelemetryAssertions(TestTelemetry subject) : base(subject) { }

    [CustomAssertion]
    public AndConstraint<TestTelemetryAssertions> HaveMessage(string key, object value, string because = "", params string[] becauseArgs)
    {
        Execute.Assertion
            .BecauseOf(because, becauseArgs)
            .ForCondition(Subject.Messages.ContainsKey(key))
            .FailWith($"Expected the Telemetry key '{key}' to be logged, but couldn't find it in:\n{Format(Subject.Messages)}");
        Execute.Assertion
            .BecauseOf(because, becauseArgs)
            .ForCondition(Subject[key].Equals(value))
            .FailWith($"Expected the Telemetry key '{key}' to be {value}, but it was {Subject[key].Equals(value)}");
        return new(this);
    }

    [CustomAssertion]
    public AndConstraint<TestTelemetryAssertions> NotHaveKey(string key, string because = "", params string[] becauseArgs)
    {
        Execute.Assertion
            .BecauseOf(because, becauseArgs)
            .ForCondition(!Subject.Messages.ContainsKey(key))
            .FailWith($"Expected the Telemetry key '{key}' to not be logged, but found it:\n{Format(Subject.Messages)}");
        return new(this);
    }

    private static string Format(IEnumerable<KeyValuePair<string, object>> messages) =>
        "\"" + string.Join("\"\n\"", messages.Select(x => $"{x.Key}: {x.Value}")) + "\"";
}
