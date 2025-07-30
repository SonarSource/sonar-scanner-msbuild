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

namespace SonarScanner.MSBuild.Common.Test;

[TestClass]
public class SerializerTests
{
    public TestContext TestContext { get; set; }

    [TestMethod]
    public void Serializer_ArgumentValidation_LoadModel()
    {
        Action act = () => Serializer.LoadModel<MyDataClass>(null);
        act.Should().Throw<ArgumentNullException>();
    }

    [TestMethod]
    public void Serializer_ArgumentValidation_ToString()
    {
        Action act = () => Serializer.ToString<MyDataClass>(null);
        act.Should().Throw<ArgumentNullException>();
    }

    [DataRow(false, "c:\\data.txt")]
    [DataRow(true, null)]
    [TestMethod]
    public void Serializer_ArgumentValidation_SaveModel_NullFileName(bool newModel, string fileName)
    {
        Action act = () => Serializer.SaveModel<MyDataClass>(newModel ? new MyDataClass() : null, fileName);
        act.Should().Throw<ArgumentNullException>();
    }

    [TestMethod]
    public void Serializer_RoundTrip_Succeeds()
    {
        var testDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        var filePath = Path.Combine(testDir, "file1.txt");
        var inputData = new MyDataClass() { Value1 = "val1", Value2 = 22 };

        Serializer.SaveModel(inputData, filePath);
        var reloaded = Serializer.LoadModel<MyDataClass>(filePath);

        reloaded.Should().NotBeNull();
        reloaded.Value1.Should().Be(inputData.Value1);
        reloaded.Value2.Should().Be(inputData.Value2);
    }

    [TestMethod]
    public void Serializer_ToString_Succeeds()
    {
        var inputData = new MyDataClass() { Value1 = "val1", Value2 = 22 };
        var actual = Serializer.ToString(inputData);

#if NETFRAMEWORK
        var expected = """
            <?xml version="1.0" encoding="utf-16"?>
            <MyDataClass xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
              <Value1>val1</Value1>
              <Value2>22</Value2>
            </MyDataClass>
            """;
#else
        var expected = """
            <?xml version="1.0" encoding="utf-16"?>
            <MyDataClass xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema">
              <Value1>val1</Value1>
              <Value2>22</Value2>
            </MyDataClass>
            """;
#endif

        actual.Should().BeIgnoringLineEndings(expected);
    }

    public class MyDataClass
    {
        public string Value1 { get; set; }
        public int Value2 { get; set; }
    }
}
