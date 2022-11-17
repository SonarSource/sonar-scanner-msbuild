/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2022 SonarSource SA
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

using System;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using SonarScanner.MSBuild.Common;

namespace SonarScanner.MSBuild.PreProcessor.Test
{
    [TestClass]
    public class CacheProcessorTests
    {
        public TestContext TestContext { get; set; }

        [TestMethod]
        public void Constructor_NullArguments_Throws()
        {
            var server = new Mock<ISonarQubeServer>().Object;
            var settings = CreateProcessedArgs();
            var logger = new Mock<ILogger>().Object;
            ((Func<CacheProcessor>)(() => new CacheProcessor(server, settings, logger))).Should().NotThrow();
            ((Func<CacheProcessor>)(() => new CacheProcessor(null, settings, logger))).Should().Throw<ArgumentNullException>().Which.ParamName.Should().Be("server");
            ((Func<CacheProcessor>)(() => new CacheProcessor(server, null, logger))).Should().Throw<ArgumentNullException>().Which.ParamName.Should().Be("settings");
            ((Func<CacheProcessor>)(() => new CacheProcessor(server, settings, null))).Should().Throw<ArgumentNullException>().Which.ParamName.Should().Be("logger");
        }

        private ProcessedArgs CreateProcessedArgs()
        {
            var processedArgs = ArgumentProcessor.TryProcessArgs(new[] {"/k:key"}, new Mock<ILogger>().Object);
            processedArgs.Should().NotBeNull();
            return processedArgs;
        }
    }
}
