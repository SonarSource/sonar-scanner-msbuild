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

using Microsoft.Build.Utilities;
using NSubstitute;

namespace SonarScanner.MSBuild.Tasks.UnitTest;

[TestClass]
public class WriteTelemetryTests
{
    public TestContext TestContext { get; set; }

    [TestMethod]
    public void AllTelemetryIsWrittenToFile()
    {
        var fileWrapper = Substitute.For<IFileWrapper>();
        var sut = new WriteTelemetry(fileWrapper)
        {
            Filename = new TaskItem("Dummy.json"),
            Key = "key1",
            Value = "value1",
            Telemetry =
            [
                new TaskItem("key2", new Dictionary<string, string>() { { "Value", "value2" } }),
                new TaskItem("key3", new Dictionary<string, string>() { { "Value", "value3" } }),
                new TaskItem("key3", new Dictionary<string, string>() { { "Value", "duplicate" } }),
                new TaskItem("key4", new Dictionary<string, string>() { { "Value", """
                        Special value with
                        NewLines
                        """ } }),
            ]
        };
        sut.Execute();
        var receivedJsonLines = (IEnumerable<string>)fileWrapper.ReceivedCalls().Should().ContainSingle().Which.GetArguments()[1];
        TestContext.WriteLine($"Received JSON lines: {string.Join(Environment.NewLine, receivedJsonLines)}");
        fileWrapper.Received(1).AppendAllLines(
            "Dummy.json",
            Arg.Is<IEnumerable<string>>(x => x.SequenceEqual(new[]
            {
                """{"key1":"value1"}""",
                """{"key2":"value2"}""",
                """{"key3":"value3"}""",
                """{"key3":"duplicate"}""",
                """{"key4":"Special value with\r\nNewLines"}""",
            })),
            Encoding.UTF8);
    }

    [TestMethod]
    public void EmptyKeysAreIgnored()
    {
        var fileWrapper = Substitute.For<IFileWrapper>();
        var sut = new WriteTelemetry(fileWrapper)
        {
            Filename = new TaskItem("Dummy.json"),
            Key = null,
            Value = "value1",
            Telemetry =
            [
                new TaskItem(string.Empty, new Dictionary<string, string>() { { "Value", "value2" } }),
                new TaskItem("key3", new Dictionary<string, string>() { { "Value", "value3" } }),
                new TaskItem(string.Empty, new Dictionary<string, string>() { { "Value", "value4" } }),
            ]
        };
        sut.Execute();
        fileWrapper.Received(1).AppendAllLines(
            "Dummy.json",
            Arg.Is<IEnumerable<string>>(static x => x.SequenceEqual(new[]
            {
                """{"key3":"value3"}""",
            })),
            Encoding.UTF8);
    }

    [TestMethod]
    public void DirectoryNotFoundIsCaught()
    {
        var dummyPath = Path.Combine("NonexistentPath", "Telemetry.json");
        var fileWrapper = Substitute.For<IFileWrapper>();
        fileWrapper.When(x => x.AppendAllLines(dummyPath, Arg.Any<IEnumerable<string>>(), Arg.Any<Encoding>()))
            .Do(_ => throw new DirectoryNotFoundException($"Could not find a part of the path '{dummyPath}'"));
        var buildEngine = new DummyBuildEngine();
        var sut = new WriteTelemetry(fileWrapper)
        {
            BuildEngine = buildEngine,
            Filename = new TaskItem(dummyPath),
            Key = "key1",
            Value = "value1",
        };
        sut.Execute();
        buildEngine.AssertSingleWarningExists($"Could not find a part of the path '{dummyPath}'");
    }

    [TestMethod]
    public void WrongMetadataProperty()
    {
        var fileWrapper = Substitute.For<IFileWrapper>();
        var buildEngine = new DummyBuildEngine();
        var sut = new WriteTelemetry(fileWrapper)
        {
            BuildEngine = buildEngine,
            Filename = new TaskItem("Dummy.json"),
            Telemetry =
            [
                new TaskItem("key1", new Dictionary<string, string>() { { "Value", "value1" } }),
                new TaskItem("key2", new Dictionary<string, string>() { { "NotValue", "value2" } }),
                new TaskItem("key3", new Dictionary<string, string>() { { "NotValue", "notValue3" }, { "Value", "value3" } }),
            ]
        };
        sut.Execute();
        fileWrapper.Received(1).AppendAllLines(
            "Dummy.json",
            Arg.Is<IEnumerable<string>>(x => x.SequenceEqual(new string[]
            {
                """{"key1":"value1"}""",
                """{"key2":""}""",
                """{"key3":"value3"}""",
            })),
            Encoding.UTF8);
    }
}
