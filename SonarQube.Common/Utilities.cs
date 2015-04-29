//-----------------------------------------------------------------------
// <copyright file="Utilities.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Diagnostics;

namespace SonarQube.Common
{
    public static class Utilities
    {
        #region Public methods

        /// <summary>
        /// Retries the specified operation until the specified timeout period expires
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="op">The operation to perform. Should return true if the operation succeeded, otherwise false.</param>
        /// <returns>True if the operation succeed, otherwise false</returns>
        public static bool Retry(int timeoutInMilliseconds, int pauseBetweenTriesInMilliseconds, ILogger logger, Func<bool> op)
        {
            if(timeoutInMilliseconds < 1)
            {
                throw new ArgumentOutOfRangeException("timeoutInMilliseconds");
            }
            if (pauseBetweenTriesInMilliseconds < 1)
            {
                throw new ArgumentOutOfRangeException("pauseBetweenTriesInMilliseconds");
            }
            if (logger == null)
            {
                throw new ArgumentNullException("logger");
            }
            if (op == null)
            {
                throw new ArgumentNullException("op");
            }
            
            logger.LogMessage(Resources.DIAG_BeginningRetry, timeoutInMilliseconds, pauseBetweenTriesInMilliseconds);

            Stopwatch timer = Stopwatch.StartNew();
            bool succeeded = op();

            while (!succeeded && timer.ElapsedMilliseconds < timeoutInMilliseconds)
            {
                logger.LogMessage(Resources.DIAG_RetryingOperation);
                System.Threading.Thread.Sleep(pauseBetweenTriesInMilliseconds);
                succeeded = op();
            }

            timer.Stop();

            if (succeeded)
            {
                logger.LogMessage(Resources.DIAG_RetryOperationSucceeded, timer.ElapsedMilliseconds);
            }
            else
            {
                logger.LogMessage(Resources.DIAG_RetryOperationFailed, timer.ElapsedMilliseconds);
            }
            return succeeded;
        }

        #endregion

    }
}
