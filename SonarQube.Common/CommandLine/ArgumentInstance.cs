/*
 * SonarQube Scanner for MSBuild
 * Copyright (C) 2016-2017 SonarSource SA
 * mailto:info AT sonarsource DOT com
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
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace SonarQube.Common
{
    /// <summary>
    /// Data class for an instance of an argument
    /// </summary>
    [DebuggerDisplay("{descriptor.Id}={value}")]
    public class ArgumentInstance
    {
        private readonly ArgumentDescriptor descriptor;
        private readonly string value;

        public ArgumentInstance(ArgumentDescriptor descriptor, string value)
        {
            this.descriptor = descriptor ?? throw new ArgumentNullException("descriptor");
            this.value = value;
        }

        #region Data

        public ArgumentDescriptor Descriptor { get { return this.descriptor; } }

        public string Value { get { return this.value; } }


        #endregion

        #region Static methods

        public static bool TryGetArgument(string id, IEnumerable<ArgumentInstance> arguments, out ArgumentInstance instance)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentNullException("id");
            }
            if (arguments == null)
            {
                throw new ArgumentNullException("arguments");
            }

            instance = arguments.FirstOrDefault(a => ArgumentDescriptor.IdComparer.Equals(a.Descriptor.Id, id));
            return instance != null;
        }

        public static bool TryGetArgumentValue(string id, IEnumerable<ArgumentInstance> arguments, out string value)
        {
            if (TryGetArgument(id, arguments, out ArgumentInstance instance))
            {
                value = instance.value;
            }
            else
            {
                value = null;
            }

            return instance != null;
        }

        #endregion
    }
}