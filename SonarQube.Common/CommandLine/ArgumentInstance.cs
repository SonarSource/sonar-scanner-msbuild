/*
 * SonarQube Scanner for MSBuild
 * Copyright (C) 2015-2017 SonarSource SA and Microsoft Corporation
 * mailto: contact AT sonarsource DOT com
 *
 * Licensed under the MIT License.
 * See LICENSE file in the project root for full license information.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
 * THE SOFTWARE.
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
            if (descriptor == null)
            {
                throw new ArgumentNullException("descriptor");
            }
            this.descriptor = descriptor;
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
            ArgumentInstance instance;
            if (TryGetArgument(id, arguments, out instance))
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