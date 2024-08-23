/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2024 SonarSource SA
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
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;

namespace SonarScanner.MSBuild.Common;

public sealed class SingleGlobalInstanceMutex : IDisposable
{
    private Mutex mutex;

    public SingleGlobalInstanceMutex(string name)
        : this(name, TimeSpan.FromSeconds(5))
    {
    }

    public SingleGlobalInstanceMutex(string name, TimeSpan acquireTimeout)
    {
        mutex = CreateMutex(name);

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

    private static Mutex CreateMutex(string name)
    {
#if NETFRAMEWORK
        // Concurrent builds could be run under different user accounts, so we need to allow all users to wait on the mutex
        var mutexSecurity = new MutexSecurity();
        mutexSecurity.AddAccessRule(new MutexAccessRule(new SecurityIdentifier(WellKnownSidType.WorldSid, null),
              MutexRights.FullControl, AccessControlType.Allow));
        return new Mutex(false, name, out var _, mutexSecurity);
#else
        return new Mutex(false, name);
#endif
    }

}
