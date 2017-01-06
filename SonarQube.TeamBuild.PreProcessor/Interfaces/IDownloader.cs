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

using System;

namespace SonarQube.TeamBuild.PreProcessor
{
    /// <summary>
    /// Interface introduced for testability
    /// </summary>
    public interface IDownloader : IDisposable
    {
        /// <summary>
        /// Attempts to download the specified page
        /// </summary>
        /// <returns>False if the url does not exist, true if the contents were downloaded successfully.
        /// Exceptions are thrown for other web failures.</returns>
        bool TryDownloadIfExists(string url, out string contents);

        /// <summary>
        /// Attempts to download the specified file
        /// </summary>
        /// <param name="targetFilePath">The file to which the downloaded data should be saved</param>
        /// <returns>False if the url does not exist, true if the data was downloaded successfully.
        /// Exceptions are thrown for other web failures.</returns>
        bool TryDownloadFileIfExists(string url, string targetFilePath);

        string Download(string url);
    }
}
