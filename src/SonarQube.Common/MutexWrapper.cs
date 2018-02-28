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
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;

namespace SonarQube.Common
{
    public sealed class MutexWrapper : IDisposable
    {
        private Mutex mutex;

        public MutexWrapper(string name)
            : this(name, TimeSpan.FromSeconds(5))
        {
        }

        public MutexWrapper(string name, TimeSpan acquireTimeout)
        {
            mutex = new Mutex(false, name);
            var allowEveryoneRule = new MutexAccessRule(new SecurityIdentifier(WellKnownSidType.WorldSid, null),
                MutexRights.FullControl, AccessControlType.Allow);
            var mutexSecurity = new MutexSecurity();
            mutexSecurity.AddAccessRule(allowEveryoneRule);
            mutex.SetAccessControl(mutexSecurity);

            try
            {
                mutex.WaitOne(acquireTimeout);
            }
            catch (AbandonedMutexException)
            {
                // This exception means the mutex was abandoned,
                // but was now acquired successfully.
            }
        }

        public void Dispose()
        {
            mutex?.ReleaseMutex();
            mutex?.Dispose();
            mutex = null;
        }
    }
}
