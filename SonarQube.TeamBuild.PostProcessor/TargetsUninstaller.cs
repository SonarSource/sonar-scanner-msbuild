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

using SonarQube.Common;
using System;
using System.IO;

namespace SonarQube.TeamBuild.PostProcessor
{
    /// <summary>
    /// Handles removing targets from well known locations
    /// </summary>
    public class TargetsUninstaller : ITargetsUninstaller
    {
        public void UninstallTargets(ILogger logger)
        {
            foreach (string directoryPath in FileConstants.ImportBeforeDestinationDirectoryPaths)
            {
                string destinationPath = Path.Combine(directoryPath, FileConstants.ImportBeforeTargetsName);

                if (!File.Exists(destinationPath))
                {
                    logger.LogDebug(Resources.MSG_UninstallTargets_NotExists, FileConstants.ImportBeforeTargetsName, directoryPath);
                    continue;
                }

                try
                {
                    File.Delete(destinationPath);
                }
                catch (IOException)
                {
                    logger.LogDebug(Resources.MSG_UninstallTargets_CouldNotDelete, FileConstants.ImportBeforeTargetsName, directoryPath);
                }
                catch (UnauthorizedAccessException)
                {
                    logger.LogDebug(Resources.MSG_UninstallTargets_CouldNotDelete, FileConstants.ImportBeforeTargetsName, directoryPath);
                }
            }
        }
    }
}