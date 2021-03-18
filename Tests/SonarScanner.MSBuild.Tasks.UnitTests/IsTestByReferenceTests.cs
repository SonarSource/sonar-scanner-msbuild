/*
 * SonarScanner for MSBuild
 * Copyright (C) 2016-2021 SonarSource SA
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
using System.Linq;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SonarScanner.MSBuild.Tasks.UnitTests
{
    [TestClass]
    public class IsTestByReferenceTests
    {
        [DataTestMethod]
        [DataRow("SimpleReference")]
        [DataRow("mscorlib, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")]
        [DataRow("mscorlib, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089", "StrongNameAndSimple")]
        public void TestReference_ProductReference_IsNull(params string[] references) =>
            ExecuteAndAssert(references, null, "Resolved test reference: ");

        [DataTestMethod]
        [DataRow(null)]
        [DataRow(new string[] { })]
        public void TestReference_EmptyReference_IsNull(string[] references) =>
            ExecuteAndAssert(references, null, "No references were resolved for current project.");

        [DataTestMethod]
        [DataRow("Microsoft.VisualStudio.TestPlatform.TestFramework")]
        [DataRow("Microsoft.VisualStudio.TestPlatform.TestFramework, Version=14.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a, processorArchitecture=MSIL")]
        [DataRow("Microsoft.VisualStudio.TestPlatform.TestFramework", "ProductionReference")]
        [DataRow("ProductionReference", "Microsoft.VisualStudio.TestPlatform.TestFramework")]
        public void TestReference_TestReference_IsTest(params string[] references) =>
            ExecuteAndAssert(references, "Microsoft.VisualStudio.TestPlatform.TestFramework", "Resolved test reference: Microsoft.VisualStudio.TestPlatform.TestFramework");

        [DataTestMethod]
        [DataRow("MOQ", "1.0")]
        [DataRow("Moq", "2.0")]
        [DataRow("MoQ", "3.0")]
        [DataRow("moq", "4.0")]
        // We need a different version for each test case, because AssemblyName implementation caches the instance and returns capitalization from the first usage
        public void TestReference_TestReference_IsTest_CaseInsensitive(string name, string version) =>
            ExecuteAndAssert(new string[] { $"{name}, Version={version}" }, name, "Resolved test reference: " + name);

        [TestMethod]
        public void TestReference_InvalidReference_IsNull()
        {
            var references = new string[] { null };
            ExecuteAndAssert(references, null, "Unable to parse assembly name: ");
        }

        private static void ExecuteAndAssert(string[] references, string expectedTestReference, string expectedLog)
        {
            var dummyEngine = new DummyBuildEngine();
            var task = new IsTestByReference { BuildEngine = dummyEngine, References = references };

            var taskSucess = task.Execute();
            taskSucess.Should().BeTrue("Expecting the task to succeed");
            dummyEngine.AssertNoErrors();
            dummyEngine.AssertNoWarnings();
            dummyEngine.AssertSingleMessageExists(expectedLog);

            task.TestReference.Should().Be(expectedTestReference);
        }
    }
}
