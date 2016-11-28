//-----------------------------------------------------------------------
// <copyright file="EncodingProvider.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using SonarQube.Common.Interfaces;
using System.Text;

namespace SonarQube.Common
{
    public class EncodingProvider : IEncodingProvider
    {
        public Encoding GetEncoding(string name)
        {
            return Encoding.GetEncoding(name);
        }

        public Encoding GetEncoding(int codepage)
        {
            return Encoding.GetEncoding(codepage);
        }
    }
}
