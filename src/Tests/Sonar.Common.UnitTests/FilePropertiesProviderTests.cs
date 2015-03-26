//-----------------------------------------------------------------------
// <copyright file="FilePropertiesProviderTests.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using TestUtilities;

namespace Sonar.Common.UnitTests
{
    [TestClass]
    public class FilePropertiesProviderTests
    {

        public TestContext TestContext { get; set; }

        #region Tests

        [TestMethod]
        public void FilePropertiesProvider_InvalidArgs()
        {
            // 1. Missing arguments
            AssertException.Expects<ArgumentNullException>(() => new FilePropertiesProvider(null));
            AssertException.Expects<ArgumentNullException>(() => new FilePropertiesProvider(string.Empty));
            AssertException.Expects<ArgumentNullException>(() => new FilePropertiesProvider("\r\t"));

            // 2. Non-existent file
            AssertException.Expects<FileNotFoundException>(() => new FilePropertiesProvider(Guid.NewGuid().ToString()));
        }

        [TestMethod]
        public void FilePropertiesProvider_EmptyFile()
        {
            // Arrange
            // File exists but has no contents
            string fullName = CreatePropertiesFile("EmptyFile.properties", string.Empty);

            // Act
            FilePropertiesProvider provider = new FilePropertiesProvider(fullName);

            // Assert
            AssertDefaultValueReturned(provider, "property1", "default1");
            AssertDefaultValueReturned(provider, "property1", null);
            AssertDefaultValueReturned(provider, "property2", "foo");
            AssertDefaultValueReturned(provider, "aaa", "123");
        }

        [TestMethod]
        public void FilePropertiesProvider_ValidProperties()
        {
            // Arrange
            string contents = @"
# commented lines should be ignored
property1=abc
#property1=should not be returned

PROPERTY1=abcUpperCase

my.property.key1=key1 Value
my.property.key2=key2 Value
my.property.key2=key2 Value Duplicate

123.456=abc.def

empty.property=

invalid name=123
invalidname2 =abc
";
            string fullName = CreatePropertiesFile("ValidProperties.properties", contents);

            // Act
            FilePropertiesProvider provider = new FilePropertiesProvider(fullName);

            // Assert
            AssertExpectedValueReturned(provider, "property1", null, "abc"); // commented-out value should be ignored
            AssertExpectedValueReturned(provider, "PROPERTY1", "123", "abcUpperCase"); // check case-sensitivity
            AssertExpectedValueReturned(provider, "my.property.key1", "xxx", "key1 Value"); // name can contain ".", value can contain spaces
            AssertExpectedValueReturned(provider, "my.property.key2", null, "key2 Value Duplicate"); // the last-defined value for a property name is used
            AssertExpectedValueReturned(provider, "123.456", "default", "abc.def"); // property name is numeric
            AssertExpectedValueReturned(provider, "empty.property", "YYY", string.Empty); // the property value is emtpy

            AssertDefaultValueReturned(provider, "invalid name", "default1"); // invalid names should not be parsed (name contains a space)
            AssertDefaultValueReturned(provider, "invalidname2 ", "default2"); // invalid names should not be parsed (space before the =)
        }

        [TestMethod]
        [Description("Tests retrieving settings from an example SonarQube project properties file")]
        public void FilePropertiesProvider_SonarProjectProperties()
        {
            // Arrange
            string contents = @"
# Note: It is not recommended to use the colon ':' character in the projectKey 
sonar.projectKey=org.example.csharpplayground
sonar.projectName=C# playground
sonar.projectVersion=1.0
 
 
sonar.sourceEncoding=UTF-8
 
 
# Disable the Visual Studio bootstrapper 
sonar.visualstudio.enable=false
sonar.sources=
sonar.exclusions=obj/**
sonar.modules=CalcAddTest,CalcMultiplyTest,CalcDivideTest,CalcSubtractTest,MyLibrary
 
# Code Coverage 
sonar.cs.ncover3.reportsPaths=coverage.nccov
sonar.cs.opencover.reportsPaths=results.xml
sonar.cs.dotcover.reportsPaths=dotCover.html
sonar.cs.vscoveragexml.reportsPaths=VisualStudio.coveragexml
 
# Unit Test Results 
sonar.cs.vstest.reportsPaths=TestResults/*.trx
  
# Required only when using SonarQube < 4.2
sonar.language=cs
";
            string fullName = CreatePropertiesFile("WellKnownProperties.properties", contents);

            // Act
            FilePropertiesProvider provider = new FilePropertiesProvider(fullName);

            // Assert
            AssertExpectedValueReturned(provider, SonarProperties.ProjectKey, "org.example.csharpplayground");
            AssertExpectedValueReturned(provider, SonarProperties.ProjectName, "C# playground");
            AssertExpectedValueReturned(provider, SonarProperties.ProjectVersion, "1.0");
            AssertExpectedValueReturned(provider, SonarProperties.SourceEncoding, "UTF-8");
        }

        [TestMethod]
        [Description("Tests retrieving settings from an example SonarQube runner property file")]
        public void FilePropertiesProvider_SonarRunnerProperties()
        {
            // Arrange
            string contents = @"
#Configure here general information about the environment, such as SonarQube DB details for example
#No information about specific project should appear here

#----- Default SonarQube server
sonar.host.url=http://tfsforsonarint.cloudapp.net:9000/

#----- PostgreSQL
#sonar.jdbc.url=jdbc:postgresql://localhost/sonar

#----- MySQL
#sonar.jdbc.url=jdbc:mysql://localhost:3306/sonar?useUnicode=true&amp;characterEncoding=utf8

#----- Oracle
#sonar.jdbc.url=jdbc:oracle:thin:@localhost/XE

#----- Microsoft SQLServer
sonar.jdbc.url=jdbc:jtds:sqlserver://SonarForTfsInt:49590/sonar;instance=SQLEXPRESS;SelectMethod=Cursor

#----- Global database settings
sonar.jdbc.username=sonar
sonar.jdbc.password=sonar

#----- Default source code encoding
sonar.sourceEncoding=UTF-8

#----- Security (when 'sonar.forceAuthentication' is set to 'true')
sonar.login=admin
sonar.password=adminpwd";

            string fullName = CreatePropertiesFile("SonarRunner.properties", contents);

            // Act
            FilePropertiesProvider provider = new FilePropertiesProvider(fullName);

            // Assert
            AssertExpectedValueReturned(provider, SonarProperties.HostUrl, "http://tfsforsonarint.cloudapp.net:9000/");
            AssertExpectedValueReturned(provider, SonarProperties.DbConnectionString, "jdbc:jtds:sqlserver://SonarForTfsInt:49590/sonar;instance=SQLEXPRESS;SelectMethod=Cursor");
            AssertExpectedValueReturned(provider, SonarProperties.DbUserName, "sonar");
            AssertExpectedValueReturned(provider, SonarProperties.DbPassword, "sonar");
            AssertExpectedValueReturned(provider, SonarProperties.SourceEncoding, "UTF-8");
            AssertExpectedValueReturned(provider, SonarProperties.SonarUserName, "admin");
            AssertExpectedValueReturned(provider, SonarProperties.SonarPassword, "adminpwd");
        }

        [TestMethod]
        [Description("Tests that the property method that does not accept a default value throws if the property is not available")]
        public void FilePropertiesProvider_GetProperty_Throws()
        {
            // Arrange
            string contents = @"
a.b.c.=exists
";
            string fullName = CreatePropertiesFile("GetProperty_Throws.properties", contents);

            // Act
            FilePropertiesProvider provider = new FilePropertiesProvider(fullName);

            // Assert
            AssertExpectedValueReturned(provider, "a.b.c.", "exists");
            Exception ex = AssertException.Expects<ArgumentException>(() => provider.GetProperty("missing.property"));

            Assert.IsTrue(ex.Message.Contains(fullName), "Expecting the error message to contain the file name");
            Assert.IsTrue(ex.Message.Contains("missing.property"), "Expecting the error message to contain the name of the requested property");
        }

        #endregion

        
        #region Private methods

        /// <summary>
        /// Creates a new text file with the specified contents in the test deployment folder
        /// </summary>
        /// <returns>Returns the full path to the new file</returns>
        private string CreatePropertiesFile(string fileName, string content)
        {
            string fullName = Path.Combine(this.TestContext.DeploymentDirectory, fileName);
            File.WriteAllText(fullName, content);
            this.TestContext.AddResultFile(fullName);
            return fullName;
        }

        private static void AssertDefaultValueReturned(FilePropertiesProvider provider, string propertyName, string suppliedDefault)
        {
            string actualValue = provider.GetProperty(propertyName, suppliedDefault);
            Assert.AreEqual(suppliedDefault, suppliedDefault, "Provider did not return the expected default value for property '{0}'", propertyName);
        }

        private static void AssertExpectedValueReturned(FilePropertiesProvider provider, string propertyName, string suppliedDefault, string expectedValue)
        {
            string actualValue = provider.GetProperty(propertyName, suppliedDefault);
            Assert.AreEqual(expectedValue, actualValue, "Provider did not return the expected value for property '{0}'", propertyName);
        }

        private static void AssertExpectedValueReturned(FilePropertiesProvider provider, string propertyName, string expectedValue)
        {
            // Both "GetProperty" methods should return the same expected value
            string actualValue = provider.GetProperty(propertyName);
            Assert.AreEqual(expectedValue, actualValue, "Provider did not return the expected value for property '{0}'", propertyName);

            actualValue = provider.GetProperty(propertyName, Guid.NewGuid().ToString() /* supply a unique default - not expecting it to be returned*/);
            Assert.AreEqual(expectedValue, actualValue, "Provider did not return the expected value for property '{0}'", propertyName);
        }

        #endregion

    }
}
