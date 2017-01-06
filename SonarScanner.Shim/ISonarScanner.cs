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

namespace SonarScanner.Shim
{
    /// <summary>
    /// Encapsulate the interaction with the sonar-scanner
    /// </summary>
    /// <remarks>Accepts the ProjectInfo.xml files as input, generates a sonar-scanner.properties 
    /// file from them, then executes the Java sonar-scanner</remarks>
    public interface ISonarScanner
    {
        ProjectInfoAnalysisResult Execute(AnalysisConfig config, IEnumerable<string> userCmdLineArguments, ILogger logger);
    }
}
