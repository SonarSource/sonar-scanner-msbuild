//-----------------------------------------------------------------------
// <copyright file="ConsoleWriter.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using System;

namespace SonarQube.Common
{
    public class ConsoleWriter : IOutputWriter
    {
        void IOutputWriter.WriteLine(string message, ConsoleColor textColor, bool isError)
        {
            using (new ConsoleColorScope(textColor))
            {
                if (isError)
                {
                    Console.Error.WriteLine(message);
                }
                else
                {
                    Console.WriteLine(message);
                }
            }
        }
    }
}
