//-----------------------------------------------------------------------
// <copyright file="IOutputWriter.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using System;

namespace SonarQube.Common
{
    /// <summary>
    /// Introduced for testability.
    /// Encapsulates the low-level write operation performed by a logger
    /// </summary>
    public interface IOutputWriter
    {
        void WriteLine(string message, ConsoleColor color, bool isError);
    }
}
