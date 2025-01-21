/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2025 SonarSource SA
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

using System.Collections;
using System.IO;
using System.Text;

namespace TestUtilities;

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
    /// Gets a reference to the ISO-8859-1 encoding (code page 28591). This is the Java standard for .properties files.
    /// </summary>
    internal static Encoding DefaultEncoding { get { return Encoding.GetEncoding(28591); } }

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
    /// <param name="defaults">A Hashtable that holds a set of defaults key value pairs to
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
        var reader = new JavaPropertyReader(this);
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
        var objectValue = this[key];
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
        var val = GetProperty(key);
        return val ?? defaultValue;
    }

    /// <summary>
    /// Set the value for a property key.  The old value is returned - if any.
    /// </summary>
    /// <param name="key">The key whose value is to be set.</param>
    /// <param name="newValue">The new value off the key.</param>
    /// <returns>The original value of the key - as a string.</returns>
    public string SetProperty(string key, string newValue)
    {
        var oldValue = AsString(this[key]);
        this[key] = newValue;
        return oldValue;
    }

    /// <summary>
    /// Returns an enumerator of all the properties available in this instance - including the
    /// defaults.
    /// </summary>
    /// <returns>An enumerator for all of the keys including defaults.</returns>
    public IEnumerator PropertyNames()
    {
        Hashtable combined;
        if (defaults != null)
        {
            combined = new Hashtable(defaults);

            for (var e = Keys.GetEnumerator(); e.MoveNext();)
            {
                var key = AsString(e.Current);
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
    private string AsString(object o)
    {
        if (o == null)
        {
            return null;
        }

        return o.ToString();
    }
}
