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

namespace SonarScanner.MSBuild.Common.Test;

[TestClass]
public class CommandLineParserTests
{
    public TestContext TestContext { get; set; }

    [TestMethod]
    public void Parser_InvalidArguments() =>
        FluentActions.Invoking(() => _ = new CommandLineParser(null, true)).Should().ThrowExactly<ArgumentNullException>();

    [TestMethod]
    public void Parser_DuplicateDescriptorIds()
    {
        var d1 = new ArgumentDescriptor("id1", ["a"], true, "desc1", false);
        var d2 = new ArgumentDescriptor("id1", ["b"], true, "desc2", false);

        FluentActions.Invoking(() => _ = new CommandLineParser([d1, d2], true)).Should().ThrowExactly<ArgumentException>();
    }

    [TestMethod]
    public void Parser_UnrecognizedArguments()
    {
        CommandLineParser parser;
        IEnumerable<ArgumentInstance> instances;
        TestLogger logger;

        var args = new[] { "/a:XXX", "/unrecognized" };

        var d1 = new ArgumentDescriptor("id1", ["/a:"], true, "desc1", false);

        // 1. Don't allow unrecognized
        parser = new CommandLineParser([d1], false);

        logger = CheckProcessingFails(parser, args);

        logger.Should().HaveErrorOnce("Unrecognized command line argument: /unrecognized")
            .And.HaveErrors(1);

        // 2. Allow unrecognized
        parser = new CommandLineParser([d1], true);
        logger = new TestLogger();
        instances = CheckProcessingSucceeds(parser, logger, args);

        AssertExpectedValue("id1", "XXX", instances);
        AssertExpectedInstancesCount(1, instances);
        logger.Should().HaveNoInfos(); // expecting unrecognized arguments to be ignored silently
    }

    [TestMethod]
    public void Parser_CaseSensitivity()
    {
        var args = new[] { "aaa:all lowercase", "AAA:all uppercase", "aAa: mixed case" };

        var d1 = new ArgumentDescriptor("id", ["AAA:"], true/* allow multiples */, "desc1", true /* allow multiple */);
        var parser = new CommandLineParser([d1], true /* allow unrecognized */);

        var instances = CheckProcessingSucceeds(parser, new TestLogger(), args);

        AssertExpectedValue("id", "all uppercase", instances);
        AssertExpectedInstancesCount(1, instances);
    }

    [TestMethod]
    public void Parser_Multiples()
    {
        CommandLineParser parser;
        IEnumerable<ArgumentInstance> instances;
        TestLogger logger;

        var args = new[] { "zzzv1", "zzzv2", "zzzv3" };

        // 1. Don't allow multiples
        var d1 = new ArgumentDescriptor("id", ["zzz"], true, "desc1", false /* no multiples */);
        parser = new CommandLineParser([d1], false);

        logger = CheckProcessingFails(parser, args);

        logger.Should().HaveErrors(
            "A value has already been supplied for this argument: zzzv2. Existing: 'v1'",
            "A value has already been supplied for this argument: zzzv3. Existing: 'v1'")
            .And.HaveErrors(2);

        // 2. Allow multiples
        d1 = new ArgumentDescriptor("id", ["zzz"], true, "desc1", true /* allow multiple */);
        parser = new CommandLineParser([d1], true);
        logger = new TestLogger();
        instances = CheckProcessingSucceeds(parser, logger, args);

        AssertExpectedValues("id", instances, "v1", "v2", "v3");
        AssertExpectedInstancesCount(3, instances);
    }

    [TestMethod]
    public void Parser_Required()
    {
        CommandLineParser parser;
        IEnumerable<ArgumentInstance> instances;
        TestLogger logger;

        // 1. Argument is required
        var d1 = new ArgumentDescriptor("id", ["AAA"], true /* required */, "desc1", false /* no multiples */);
        parser = new CommandLineParser([d1], false);

        logger = CheckProcessingFails(parser);

        logger.Should().HaveErrorOnce("A required argument is missing: desc1")
            .And.HaveErrors(1);

        // 2. Argument is not required
        d1 = new ArgumentDescriptor("id", ["AAA"], false /* not required */, "desc1", false /* no multiples */);
        parser = new CommandLineParser([d1], true);
        logger = new TestLogger();
        instances = CheckProcessingSucceeds(parser, logger);

        AssertExpectedInstancesCount(0, instances);
    }

    [TestMethod]
    public void Parser_Verbs_ExactMatchesOnly()
    {
        CommandLineParser parser;
        IEnumerable<ArgumentInstance> instances;
        TestLogger logger;

        var verb1 = new ArgumentDescriptor("v1", ["begin"], false /* not required */, "desc1", false /* no multiples */, true);
        parser = new CommandLineParser([verb1], true /* allow unrecognized */);

        // 1. Exact match -> matched
        logger = new TestLogger();
        instances = CheckProcessingSucceeds(parser, logger, "begin");
        AssertExpectedValue("v1", string.Empty, instances);
        AssertExpectedInstancesCount(1, instances);

        // 2. Partial match -> not matched
        logger = new TestLogger();
        instances = CheckProcessingSucceeds(parser, logger, "beginX");
        AssertExpectedInstancesCount(0, instances);

        // 3. Combination -> only exact matches matched
        logger = new TestLogger();
        instances = CheckProcessingSucceeds(parser, logger, "beginX", "begin", "beginY");
        instances.First().Value.Should().Be(string.Empty, "Value for verb should be empty");
        AssertExpectedInstancesCount(1, instances);
        AssertExpectedValue("v1", string.Empty, instances);
    }

    [TestMethod]
    public void Parser_Verbs_Multiples()
    {
        CommandLineParser parser;
        IEnumerable<ArgumentInstance> instances;
        TestLogger logger;

        var verb1 = new ArgumentDescriptor("v1", ["noMult"], false /* required */, "noMult desc", false /* no multiples */, true);
        var verb2 = new ArgumentDescriptor("v2", ["multOk"], false /* required */, "multOk desc", true /* allow multiples */, true);

        parser = new CommandLineParser([verb1, verb2], true /* allow unrecognized */);

        // 1. Allowed multiples
        logger = new TestLogger();
        instances = CheckProcessingSucceeds(parser, logger, "multOk", "multOk");
        AssertExpectedInstancesCount(2, instances);

        // 2. Disallowed multiples
        logger = CheckProcessingFails(parser, "noMult", "noMult");
        logger.Should().HaveErrorOnce("A value has already been supplied for this argument: noMult. Existing: ''")
            .And.HaveErrors(1);
    }

    [TestMethod]
    public void Parser_Verbs_Required()
    {
        CommandLineParser parser;
        IEnumerable<ArgumentInstance> instances;
        TestLogger logger;

        var matchingPrefixArgs = new[] { "AAAa" };

        // 1a. Argument is required but is missing -> error
        var d1 = new ArgumentDescriptor("id", ["AAA"], true /* required */, "desc1", false /* no multiples */, true);
        parser = new CommandLineParser([d1], false);
        logger = CheckProcessingFails(parser);

        logger.Should().HaveErrorOnce("A required argument is missing: desc1")
            .And.HaveErrors(1);

        // 1b. Argument is required but is only partial match -> missing -> error2
        logger = CheckProcessingFails(parser, matchingPrefixArgs);

        logger.Should().HaveErrors(
            "A required argument is missing: desc1",
            "Unrecognized command line argument: AAAa")
            .And.HaveErrors(2);

        // 2a. Argument is not required, missing -> ok
        d1 = new ArgumentDescriptor("id", ["AAA"], false /* not required */, "desc1", false /* no multiples */, true);
        parser = new CommandLineParser([d1], true);
        logger = new TestLogger();
        instances = CheckProcessingSucceeds(parser, logger);

        AssertExpectedInstancesCount(0, instances);

        // 2b. Argument is not required, partial -> missing -> ok
        logger = new TestLogger();
        instances = CheckProcessingSucceeds(parser, logger, matchingPrefixArgs);

        AssertExpectedInstancesCount(0, instances);
    }

    [TestMethod]
    public void Parser_OverlappingVerbsAndPrefixes()
    {
        // Tests handling of verbs and non-verbs that start with the same values
        CommandLineParser parser;
        IEnumerable<ArgumentInstance> instances;
        TestLogger logger;

        var verb1 = new ArgumentDescriptor("v1", ["X"], false /* not required */, "verb1 desc", false /* no multiples */, true);
        var prefix1 = new ArgumentDescriptor("p1", ["XX"], false /* not required */, "prefix1 desc", false /* no multiples */, false);
        var verb2 = new ArgumentDescriptor("v2", ["XXX"], false /* not required */, "verb2 desc", false /* no multiples */, true);
        var prefix2 = new ArgumentDescriptor("p2", ["XXXX"], false /* not required */, "prefix2 desc", false /* no multiples */, false);

        // NOTE: this test only works because the descriptors are supplied to parser ordered
        // by decreasing prefix length
        parser = new CommandLineParser([prefix2, verb2, prefix1, verb1], true /* allow unrecognized */);

        // 1. Exact match -> matched
        logger = new TestLogger();
        instances = CheckProcessingSucceeds(
            parser,
            logger,
            "X",        // verb 1 - exact match
            "XXAAA",    // prefix 1 - has value A,
            "XXX",      // verb 2 - exact match,
            "XXXXB");   // prefix 2 - has value B

        AssertExpectedValue("v1", string.Empty, instances);
        AssertExpectedValue("p1", "AAA", instances);
        AssertExpectedValue("v2", string.Empty, instances);
        AssertExpectedValue("p2", "B", instances);
    }

    private static IEnumerable<ArgumentInstance> CheckProcessingSucceeds(CommandLineParser parser, TestLogger logger, params string[] args)
    {
        var success = parser.ParseArguments(args, logger, out var instances);
        success.Should().BeTrue("Expecting parsing to succeed");
        instances.Should().NotBeNull("Instances should not be null if parsing succeeds");
        logger.Should().HaveNoErrors();
        return instances;
    }

    private static TestLogger CheckProcessingFails(CommandLineParser parser, params string[] args)
    {
        var logger = new TestLogger();
        var success = parser.ParseArguments(args, logger, out var instances);

        success.Should().BeFalse("Expecting parsing to fail");
        instances.Should().NotBeNull("Instances should not be null even if parsing fails");

        logger.Should().HaveErrors();

        return logger;
    }

    private static void AssertExpectedInstancesCount(int expected, IEnumerable<ArgumentInstance> actual) =>
        actual.Should().HaveCount(expected, "Unexpected number of arguments recognized");

    private static void AssertExpectedValue(string id, string expectedValue, IEnumerable<ArgumentInstance> actual)
    {
        var found = ArgumentInstance.TryGetArgument(id, actual, out var actualInstance);
        found.Should().BeTrue("Expected argument was not found. Id: {0}", id);
        actual.Should().NotBeNull();

        actualInstance.Value.Should().Be(expectedValue, "Unexpected instance value. Id: {0}", id);

        actual.Where(x => ArgumentDescriptor.IdComparer.Equals(x.Descriptor.Id, id)).Select(x => x.Value)
            .Should().ContainSingle("Not expecting to find multiple values. Id: {0}", id);
    }

    private static void AssertExpectedValues(string id, IEnumerable<ArgumentInstance> actual, params string[] expectedValues) =>
        actual.Where(x => ArgumentDescriptor.IdComparer.Equals(x.Descriptor.Id, id)).Select(x => x.Value)
            .Should().BeEquivalentTo(expectedValues);
}
