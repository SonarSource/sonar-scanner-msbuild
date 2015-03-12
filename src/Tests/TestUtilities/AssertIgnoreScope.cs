//-----------------------------------------------------------------------
// <copyright file="AssertIgnoreScope.cs" company="SonarSource SA and Microsoft Corporation">
//   (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Diagnostics;
using System.Linq;

namespace TestUtilities
{
    /// <summary>
    /// Helper class to suppress assertions during tests
    /// </summary>
    /// <remarks>Prevents tests from failing due to assertion dialogues appearing</remarks>
    public class AssertIgnoreScope : IDisposable
    {
        public AssertIgnoreScope()
        {
            SetAssertUIEnabled(false);
        }

        private static void SetAssertUIEnabled(bool enable)
        {
            DefaultTraceListener listener = Debug.Listeners.OfType<DefaultTraceListener>().FirstOrDefault();
            Debug.Assert(listener != null, "Failed to locate the default trace listener");
            if (listener != null)
            {
                listener.AssertUiEnabled = enable;
            }
        }

        #region IDisposable Support

        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    SetAssertUIEnabled(true);
                }

                disposedValue = true;
            }
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
        }

        #endregion
    }
}
