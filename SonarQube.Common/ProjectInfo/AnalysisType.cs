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

namespace SonarQube.Common
{
    /* If we move to a plug-in model (i.e. so handlers for new types of analyzers
       can be plugged in at runtime e.g. using MEF) then this enum would be removed.
       For the time being we are only supported a known set of analyzers.
    */

    /// <summary>
    /// Lists the known types of analyzers that are handled by the properties generator
    /// </summary>
    public enum AnalysisType
    {
        /// <summary>
        /// List of files that should be analyzed
        /// </summary>
        /// <remarks>The files could be of any type and any language</remarks>
        FilesToAnalyze,

        /// <summary>
        /// An FxCop results file
        /// </summary>
        FxCop,

        /// <summary>
        /// An XML code coverage report produced by the Visual Studio code coverage tool
        /// </summary>
        VisualStudioCodeCoverage
    }
}
