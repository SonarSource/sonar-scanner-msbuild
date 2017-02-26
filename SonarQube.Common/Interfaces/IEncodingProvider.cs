//-----------------------------------------------------------------------
// <copyright file="IEncodingProvider.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using System.Text;

namespace SonarQube.Common.Interfaces
{
    public interface IEncodingProvider
    {
        Encoding GetEncoding(int codepage);

        Encoding GetEncoding(string name);
    }
}
