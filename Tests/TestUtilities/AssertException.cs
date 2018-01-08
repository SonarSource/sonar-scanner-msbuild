/*
 * SonarQube Scanner for MSBuild
 * Copyright (C) 2016-2018 SonarSource SA
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
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TestUtilities
{
    public static class AssertException
    {
        #region Public methods

        /// <summary>
        /// Asserts that the expected exception is thrown
        /// </summary>
        public static TException Expects<TException>(Action op) where TException : Exception
        {
            Assert.IsNotNull("Test error: supplied operation cannot be null");

            var expectedType = typeof(TException);
            Exception caught = null;
            try
            {
                op();
            }
            catch(Exception ex)
            {
                caught = ex;
            }

            Assert.IsNotNull(caught, "Expecting an exception to be thrown. Expected: {0}", expectedType.FullName);
            Assert.IsInstanceOfType(caught, expectedType, "Unexpected exception thrown");

            return (TException)caught;
        }

        #endregion Public methods
    }
}
