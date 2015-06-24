//-----------------------------------------------------------------------
// <copyright file="ArgumentInstance.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using System;

namespace SonarQube.Common
{
    /// <summary>
    /// Data class for an instance of an argument
    /// </summary>
    public class ArgumentInstance
    {
        private readonly ArgumentDescriptor descriptor;
        private readonly string value;

        public ArgumentInstance(ArgumentDescriptor descriptor, string value)
        {
            if (descriptor == null)
            {
                throw new ArgumentNullException("descriptor");
            }
            this.descriptor = descriptor;
            this.value = value;
        }

        public ArgumentDescriptor Descriptor { get { return this.descriptor; } }

        public string Value { get { return this.value; } }
    }
}