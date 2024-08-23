/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2024 SonarSource SA
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

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Microsoft.Build.Framework;

namespace SonarScanner.MSBuild.Tasks.UnitTest;

public sealed class DummyBuildEngine : IBuildEngine
{
    private readonly List<BuildWarningEventArgs> warnings;
    private readonly List<BuildErrorEventArgs> errors;
    private readonly List<BuildMessageEventArgs> messages;

    #region Public methods

    public DummyBuildEngine()
    {
        warnings = new List<BuildWarningEventArgs>();
        errors = new List<BuildErrorEventArgs>();
        messages = new List<BuildMessageEventArgs>();
    }

    public IReadOnlyList<BuildErrorEventArgs> Errors { get { return errors.AsReadOnly(); } }

    public IReadOnlyList<BuildWarningEventArgs> Warnings { get { return warnings.AsReadOnly(); } }

    #endregion Public methods

    #region IBuildEngine interface

    bool IBuildEngine.BuildProjectFile(string projectFileName, string[] targetNames, IDictionary globalProperties,
        IDictionary targetOutputs)
    {
        throw new NotImplementedException();
    }

    int IBuildEngine.ColumnNumberOfTaskNode
    {
        get { return -2; }
    }

    bool IBuildEngine.ContinueOnError
    {
        get { return false; }
    }

    int IBuildEngine.LineNumberOfTaskNode
    {
        get { return -1; }
    }

    void IBuildEngine.LogCustomEvent(CustomBuildEventArgs e)
    {
        throw new NotImplementedException();
    }

    void IBuildEngine.LogErrorEvent(BuildErrorEventArgs e)
    {
        Console.WriteLine("BuildEngine: ERROR: {0}", e.Message);
        errors.Add(e);
    }

    void IBuildEngine.LogMessageEvent(BuildMessageEventArgs e)
    {
        Console.WriteLine("BuildEngine: MESSAGE: {0}", e.Message);
        messages.Add(e);
    }

    void IBuildEngine.LogWarningEvent(BuildWarningEventArgs e)
    {
        Console.WriteLine("BuildEngine: WARNING: {0}", e.Message);
        warnings.Add(e);
    }

    string IBuildEngine.ProjectFileOfTaskNode
    {
        get { return null; }
    }

    #endregion IBuildEngine interface

    #region Assertions

    public void AssertNoErrors()
    {
        errors.Should().BeEmpty("Not expecting any errors to have been logged");
    }

    public void AssertNoWarnings()
    {
        warnings.Should().BeEmpty("Not expecting any warnings to have been logged");
    }

    /// <summary>
    /// Checks that a single error exists that contains all of the specified strings
    /// </summary>
    public void AssertSingleErrorExists(params string[] expected)
    {
        errors.Should().ContainSingle(w => expected.All(e => w.Message.Contains(e)), "More than one error contains the expected strings: {0}", string.Join(",", expected));
    }

    /// <summary>
    /// Checks that at least one message exists that contains all of the specified strings.
    /// </summary>
    public void AssertSingleMessageExists(params string[] expected)
    {
        messages.Should().ContainSingle(m => expected.All(e => m.Message.Contains(e)), "More than one message contains the expected strings: {0}", string.Join(",", expected));
    }

    /// <summary>
    /// Checks that at least one warning exists that contains all of the specified strings.
    /// </summary>
    public void AssertSingleWarningExists(params string[] expected)
    {
        warnings.Should().ContainSingle(w => expected.All(e => w.Message.Contains(e)), "More than one warning contains the expected strings: {0}", string.Join(",", expected));
    }

    #endregion Assertions
}
