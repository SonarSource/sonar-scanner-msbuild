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
namespace SonarQube.TeamBuild.Integration
{
    public interface ICoverageReportConverter // was internal
    {
        /// <summary>
        /// Initialises the converter
        /// </summary>
        /// <returns>True if the converter was initialised successfully, otherwise false</returns>
        bool Initialize(ILogger logger);

        /// <summary>
        /// Converts the supplied binary code coverage report file to XML
        /// </summary>
        /// <param name="inputFilePath">The full path to the binary file to be converted</param>
        /// <param name="outputFilePath">The name of the XML file to be created</param>
        /// <returns>True if the conversion was successful, otherwise false</returns>
        bool ConvertToXml(string inputFilePath, string outputFilePath, ILogger logger);
    }
}
