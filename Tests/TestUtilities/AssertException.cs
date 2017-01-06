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
        public static TException Expects<TException>(Action op) where TException : Exception
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

            return (TException)caught;
        }

        #endregion
    }
}
