/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2025 SonarSource Sàrl
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

using Newtonsoft.Json.Linq;

namespace TestUtilities;

public class ScannerEngineInputReader
{
    private readonly Dictionary<string, string> properties = new();

    public string this[string key] =>
        properties.TryGetValue(key, out var value) ? value : null;

    public ScannerEngineInputReader(string content)
    {
        _ = content ?? throw new ArgumentNullException(nameof(content));
        var json = JObject.Parse(content);
        if (json["scannerProperties"] is JArray jsonProperties)
        {
            foreach (var property in jsonProperties)
            {
                var key = property["key"].Should().NotBeNull().And.Subject.Value<string>();
                var value = property["value"].Should().NotBeNull().And.Subject.Value<string>();
                properties.Add(key, value); // Duplicate keys should throw here
            }
        }
        else
        {
            Assert.Fail("Scanner Engine Input does not contain expected 'scannerProperties' field.");
        }
    }

    public void AssertProperty(string key, string expectedValue) =>
        this[key].Should().Be(expectedValue, "Scanner Engine Input should contain key '{0}'", key);

    public void AssertPropertyDoesNotExist(string key) =>
        this[key].Should().BeNull("Scanner Engine Input should not contain key '{0}'", key);
}
