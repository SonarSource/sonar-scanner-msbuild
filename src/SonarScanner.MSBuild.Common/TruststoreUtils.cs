/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2025 SonarSource SA
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

using System.Security.Cryptography.X509Certificates;

namespace SonarScanner.MSBuild.Common;

public static class TruststoreUtils
{
    // There is 2 possible values for the default password.
    // During the begin step, it would be easy to determine which one to use:
    // we try all the possible passwords and use the one that works.
    // However, during the end step, we need to know which password to use as we set it before
    // invoking the scanner CLI.
    //
    // This method tries all default passwords and returns the first one that works.
    // If none of them works, it returns the first one.
    // We ignore the failures and let it happened either when we build the WebClientDownloader or
    // during the Scanner CLI execution.
    public static string TruststoreDefaultPassword(string truststorePath, ILogger logger)
    {
        if (truststorePath is not null)
        {
            for (var idx = 0; idx < SonarPropertiesDefault.TruststorePasswords.Count; idx++)
            {
                var truststorePassword = SonarPropertiesDefault.TruststorePasswords[idx];
                var trustStore = new X509Certificate2Collection();
                try
                {
                    trustStore.Import(truststorePath.Trim('\"'), truststorePassword, X509KeyStorageFlags.DefaultKeySet);
                    return truststorePassword;
                }
                // On MacOS, if the certificate is not found on disk, the import will fail with FileNotFoundException
                catch (Exception ex)
                {
                    // We ignore any exception and try the next password
                    logger.LogDebug(Resources.MSG_CouldNotImportTruststoreWithDefaultPassword, truststorePath, idx, ex.Message);
                }
            }
        }

        return SonarPropertiesDefault.TruststorePasswords[0];
    }
}
