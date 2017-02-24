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
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestUtilities
{
    /// <summary>
    /// Hold Java style properties as key-value pairs and allow them to be loaded from or
    /// saved to a ".properties" file. The file is stored with character set ISO-8859-1 which extends US-ASCII
    /// (the characters 0-127 are the same) and forms the first part of the Unicode character set.  Within the
    /// application <see cref="string"/> are Unicode - but all values outside the basic US-ASCII set are escaped.
    /// </summary>
    /// Copied from https://github.com/Kajabity/Kajabity-Tools
    public class JavaProperties : Hashtable
    {
        /// <summary>
        /// Gets a reference to the ISO-8859-1 encoding (code page 28592). This is the Java standard for .properties files.
        /// </summary>
        internal static Encoding DefaultEncoding { get { return Encoding.GetEncoding(28592); } }

        /// <summary>
        /// A reference to an optional set of default properties - these values are returned
        /// if the value has not been loaded from a ".properties" file or set programatically.
        /// </summary>
        protected Hashtable defaults;

        /// <summary>
        /// An empty constructor that doesn't set the defaults.
        /// </summary>
        public JavaProperties()
        {
        }

        /// <summary>
        /// Use this constructor to provide a set of default values.  The default values are kept separate
        /// to the ones in this instant.
        /// </summary>
        /// <param name="defaults">A Hashtable that holds a set of defafult key value pairs to
        /// return when the requested key has not been set.</param>
        public JavaProperties(Hashtable defaults)
        {
            this.defaults = defaults;
        }

        /// <summary>
        /// Load Java Properties from a stream expecting the format as described in <see cref="JavaPropertyReader"/>.
        /// </summary>
        /// <param name="streamIn">An input stream to read properties from.</param>
        /// <exception cref="ParseException">If the stream source is invalid.</exception>
        public void Load(Stream streamIn)
        {
            JavaPropertyReader reader = new JavaPropertyReader(this);
            reader.Parse(streamIn);
        }

        /// <summary>
        /// Get the value for the specified key value.  If the key is not found, then return the
        /// default value - and if still not found, return null.
        /// </summary>
        /// <param name="key">The key whose value should be returned.</param>
        /// <returns>The value corresponding to the key - or null if not found.</returns>
        public string GetProperty(string key)
        {
            Object objectValue = this[key];
            if (objectValue != null)
            {
                return AsString(objectValue);
            }
            else if (defaults != null)
            {
                return AsString(defaults[key]);
            }

            return null;
        }

        /// <summary>
        /// Get the value for the specified key value.  If the key is not found, then return the
        /// default value - and if still not found, return <c>defaultValue</c>.
        /// </summary>
        /// <param name="key">The key whose value should be returned.</param>
        /// <param name="defaultValue">The default value if the key is not found.</param>
        /// <returns>The value corresponding to the key - or null if not found.</returns>
        public string GetProperty(string key, string defaultValue)
        {
            string val = GetProperty(key);
            return (val == null) ? defaultValue : val;
        }

        /// <summary>
        /// Set the value for a property key.  The old value is returned - if any.
        /// </summary>
        /// <param name="key">The key whose value is to be set.</param>
        /// <param name="newValue">The new value off the key.</param>
        /// <returns>The original value of the key - as a string.</returns>
        public string SetProperty(string key, string newValue)
        {
            string oldValue = AsString(this[key]);
            this[key] = newValue;
            return oldValue;
        }

        /// <summary>
        /// Returns an enumerator of all the properties available in this instance - including the
        /// defaults.
        /// </summary>
        /// <returns>An enumarator for all of the keys including defaults.</returns>
        public IEnumerator PropertyNames()
        {
            Hashtable combined;
            if (defaults != null)
            {
                combined = new Hashtable(defaults);

                for (IEnumerator e = this.Keys.GetEnumerator(); e.MoveNext();)
                {
                    string key = AsString(e.Current);
                    combined.Add(key, this[key]);
                }
            }
            else
            {
                combined = new Hashtable(this);
            }

            return combined.Keys.GetEnumerator();
        }

        /// <summary>
        /// A utility method to safely convert an <c>Object</c> to a <c>string</c>.
        /// </summary>
        /// <param name="o">An Object or null to be returned as a string.</param>
        /// <returns>string value of the object - or null.</returns>
        private string AsString(Object o)
        {
            if (o == null)
            {
                return null;
            }

            return o.ToString();
        }
    }
}
