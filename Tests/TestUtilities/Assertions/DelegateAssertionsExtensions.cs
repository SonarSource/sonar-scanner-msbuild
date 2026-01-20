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

using System.Runtime.InteropServices;
using FluentAssertions.Execution;
using FluentAssertions.Specialized;

namespace TestUtilities.Assertions;

public static class DelegateAssertionsExtensions
{
    public static ExceptionAssertions<Exception> ThrowOSBased<TWindowsException, TLinuxException, TMacException>(this DelegateAssertions<Action, ActionAssertions> assertions)
        where TWindowsException : Exception
        where TLinuxException : Exception
        where TMacException : Exception
    {
        var expectedExceptionType = GetExpectedExceptionType<TWindowsException, TLinuxException, TMacException>();

        Execute.Assertion
            .ForCondition(assertions.Subject is not null)
            .FailWith("Expected a delegate, but found null.");

        try
        {
            assertions.Subject();
            Execute.Assertion
                .FailWith("Expected {context:delegate} to throw {0}, but it did not throw any exception.", expectedExceptionType);
        }
        catch (Exception ex)
        {
            var actualExceptionType = ex.GetType();
            Execute.Assertion
                .ForCondition(actualExceptionType == expectedExceptionType || expectedExceptionType.IsAssignableFrom(actualExceptionType))
                .FailWith("Expected {context:delegate} to throw {0}, but {1} was thrown.", expectedExceptionType, actualExceptionType);

            return new ExceptionAssertions<Exception>([ex]);
        }
        return new ExceptionAssertions<Exception>([]);
    }

    private static Type GetExpectedExceptionType<TWindowsException, TLinuxException, TMacOSException>()
        where TWindowsException : Exception
        where TLinuxException : Exception
        where TMacOSException : Exception
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return typeof(TWindowsException);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return typeof(TLinuxException);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return typeof(TMacOSException);
        }
        else
        {
            throw new NotSupportedException("Unsupported operating system.");
        }
    }
}
