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
public class WriteSonarTelemetryTests
{
    public TestContext TestContext { get; set; }

    [TestMethod]
    public void AllTelemetryIsWrittenToFile()
    {
        var fileWrapper = Substitute.For<IFileWrapper>();
        var sut = new WriteSonarTelemetry(fileWrapper)
        {
            Filename = new TaskItem("Dummy.json"),
            Key = "key1",
            Value = "value1",
            Telemetry =
            [
                TelemetryTaskItem("key2", "value2"),
                TelemetryTaskItem("key3", "value3"),
                TelemetryTaskItem("key3", "duplicate"),
                TelemetryTaskItem("key4", """
                    Special value with
                    NewLines
                    """),
            ]
        };
        sut.Execute();
        PrintReceivedJsonToTestContext(fileWrapper);
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
        fileWrapper.DidNotReceive().CreateNewAllLines(Arg.Any<string>(), Arg.Any<IEnumerable<string>>(), Arg.Any<Encoding>());
    }

    [TestMethod]
    public void EmptyKeysAreIgnored()
    {
        var fileWrapper = Substitute.For<IFileWrapper>();
        var sut = new WriteSonarTelemetry(fileWrapper)
        {
            Filename = new TaskItem("Dummy.json"),
            Key = null,
            Value = "value1",
            Telemetry =
            [
                TelemetryTaskItem(string.Empty, "value2"),
                TelemetryTaskItem("key3", "value3"),
                TelemetryTaskItem(string.Empty, "value4"),
            ]
        };
        sut.Execute();
        PrintReceivedJsonToTestContext(fileWrapper);
        fileWrapper.Received(1).AppendAllLines(
            "Dummy.json",
            Arg.Is<IEnumerable<string>>(static x => x.SequenceEqual(new[]
            {
                """{"key3":"value3"}""",
            })),
            Encoding.UTF8);
        fileWrapper.DidNotReceive().CreateNewAllLines(Arg.Any<string>(), Arg.Any<IEnumerable<string>>(), Arg.Any<Encoding>());
    }

    [TestMethod]
    public void DirectoryNotFoundIsCaught()
    {
        var dummyPath = Path.Combine("NonexistentPath", "Telemetry.json");
        var fileWrapper = Substitute.For<IFileWrapper>();
        fileWrapper.When(x => x.AppendAllLines(dummyPath, Arg.Any<IEnumerable<string>>(), Arg.Any<Encoding>()))
            .Do(_ => throw new DirectoryNotFoundException($"Could not find a part of the path '{dummyPath}'"));
        var buildEngine = new DummyBuildEngine();
        var sut = new WriteSonarTelemetry(fileWrapper)
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
        var sut = new WriteSonarTelemetry(fileWrapper)
        {
            BuildEngine = buildEngine,
            Filename = new TaskItem("Dummy.json"),
            Telemetry =
            [
                new TaskItem("key1", new Dictionary<string, string> { { "Value", "value1" } }),
                new TaskItem("key2", new Dictionary<string, string> { { "NotValue", "value2" } }),
                new TaskItem("key3", new Dictionary<string, string> { { "NotValue", "notValue3" }, { "Value", "value3" } }),
            ]
        };
        sut.Execute();
        PrintReceivedJsonToTestContext(fileWrapper);
        fileWrapper.Received(1).AppendAllLines(
            "Dummy.json",
            Arg.Is<IEnumerable<string>>(x => x.SequenceEqual(new string[]
            {
                """{"key1":"value1"}""",
                """{"key2":""}""",
                """{"key3":"value3"}""",
            })),
            Encoding.UTF8);
        fileWrapper.DidNotReceive().CreateNewAllLines(Arg.Any<string>(), Arg.Any<IEnumerable<string>>(), Arg.Any<Encoding>());
    }

    [TestMethod]
    public void CreateNewCaugthIoException()
    {
        var fileWrapper = Substitute.For<IFileWrapper>();
        var buildEngine = new DummyBuildEngine();
        var sut = new WriteSonarTelemetry(fileWrapper)
        {
            BuildEngine = buildEngine,
            Filename = new TaskItem("Dummy.json"),
            CreateNew = true,
            Telemetry =
            [
                new TaskItem("key1", new Dictionary<string, string> { { "Value", "value1" } }),
            ]
        };
        fileWrapper.When(x => x.CreateNewAllLines("Dummy.json", Arg.Any<IEnumerable<string>>(), Encoding.UTF8)).Throw<IOException>();
        sut.Execute();
        fileWrapper.Received(1).CreateNewAllLines(
            "Dummy.json",
            Arg.Is<IEnumerable<string>>(x => x.SequenceEqual(new string[]
            {
                """{"key1":"value1"}""",
            })),
            Encoding.UTF8);
        fileWrapper.DidNotReceive().AppendAllLines(Arg.Any<string>(), Arg.Any<IEnumerable<string>>(), Arg.Any<Encoding>());
        buildEngine.AssertNoWarnings();
    }

    [TestMethod]
    public void CreateNewOnlySingleFileIsCreated()
    {
        var fileWrapper = FileWrapper.Instance; // Use the real file wrapper to test the exception handling logic.
        var buildEngine = new DummyBuildEngine();
        var basePath = TestContext.TestRunDirectory;
        var testFile = Path.Combine(basePath, "Telemetry.json");
        var sut = new WriteSonarTelemetry(fileWrapper)
        {
            BuildEngine = buildEngine,
            Filename = new TaskItem(testFile),
            CreateNew = true,
            Key = "key1",
            Value = "value1",
        };
        ExecuteInParallel(sut, 100);
        File.ReadAllLines(testFile).Should().BeEquivalentTo("""{"key1":"value1"}"""); // only a single Task should win the race and the other task should not touch the file.
        sut.Key = "otherKey";
        sut.Value = "otherValue";
        ExecuteInParallel(sut, 100);
        File.ReadAllLines(testFile).Should().BeEquivalentTo("""{"key1":"value1"}"""); // the file already exists and is not touched. The new kvp is neither appended nor replaces the existing extry.
        buildEngine.AssertNoWarnings();

        // Simulate parallel builds that write to the telemetry file.
        static void ExecuteInParallel(WriteSonarTelemetry sut, int parallelTasks) =>
            Parallel.For(0, parallelTasks, new ParallelOptions { MaxDegreeOfParallelism = parallelTasks }, _ => sut.Execute());
    }

    private void PrintReceivedJsonToTestContext(IFileWrapper fileWrapper)
    {
        var receivedCall = fileWrapper.ReceivedCalls().Should().ContainSingle().Which;
        receivedCall.GetMethodInfo().Name.Should().Be(nameof(IFileWrapper.AppendAllLines));
        var receivedJsonLines = receivedCall.GetArguments().Should().HaveCount(3).And.Subject.ElementAt(1).Should().BeAssignableTo<IEnumerable<string>>().Subject;
        TestContext.WriteLine($"Received JSON lines:{Environment.NewLine}{string.Join(Environment.NewLine, receivedJsonLines)}");
    }

    private static TaskItem TelemetryTaskItem(string key, string value) =>
        new(key, new Dictionary<string, string> { { "Value", value } });
}
