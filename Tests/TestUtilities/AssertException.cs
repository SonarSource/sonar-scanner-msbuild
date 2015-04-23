//-----------------------------------------------------------------------
// <copyright file="AssertException.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace TestUtilities
{
    public static class AssertException
    {
        #region Public methods

        /// <summary>
        /// Asserts that the expected exception is thrown
        /// </summary>
        public static Exception Expects<TException>(Action op) where TException : Exception
        {
            Assert.IsNotNull("Test error: supplied operation cannot be null");

            Type expectedType = typeof(TException);
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

            return caught;
        }

        #endregion
    }
}
