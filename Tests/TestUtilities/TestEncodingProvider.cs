//-----------------------------------------------------------------------
// <copyright file="TestEncodingProvider.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using SonarQube.Common.Interfaces;
using System;
using System.Text;

namespace TestUtilities
{
    public class TestEncodingProvider : IEncodingProvider
    {
        private Func<int, Encoding> _intEncodingFunc;
        private Func<string, Encoding> _stringEncodingFunc;

        public TestEncodingProvider(Func<int, Encoding> intEncodingFunc, Func<string, Encoding> stringEncodingFunc)
        {
            _intEncodingFunc = intEncodingFunc;
            _stringEncodingFunc = stringEncodingFunc;
        }

        public Encoding GetEncoding(string name)
        {
            return _stringEncodingFunc(name);
        }

        public Encoding GetEncoding(int codepage)
        {
            return _intEncodingFunc(codepage);
        }
    }
}
