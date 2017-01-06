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
using System.Collections.Generic;

namespace SonarQube.TeamBuild.Integration
{
    public interface ICoverageUrlProvider // was internal
    {
        /// <summary>
        /// Builds and returns the download URLs for all code coverage reports for the specified build
        /// </summary>
        /// <param name="tfsUri">The URI of the TFS collection</param>
        /// <parparam name="buildUri">The URI of the build for which data should be retrieved</parparam>
        IEnumerable<string> GetCodeCoverageReportUrls(string tfsUri, string buildUri, ILogger logger);
    }
}
