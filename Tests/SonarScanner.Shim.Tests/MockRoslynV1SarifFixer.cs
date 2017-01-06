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

namespace SonarScanner.Shim.Tests
{
    class MockRoslynV1SarifFixer : IRoslynV1SarifFixer
    {

        #region Test Hooks

        public string ReturnVal { get; set; }

        public int CallCount { get; set; }

        public string LastLanguage { get; set; }

        public MockRoslynV1SarifFixer(string returnVal)
        {
            this.ReturnVal = returnVal;
            this.CallCount = 0;
        }

        #endregion

        #region IRoslynV1SarifFixer

        public string LoadAndFixFile(string sarifPath, string language, ILogger logger)
        {
            CallCount++;
            LastLanguage = language;
            return ReturnVal;
        }

        #endregion
    }
}
