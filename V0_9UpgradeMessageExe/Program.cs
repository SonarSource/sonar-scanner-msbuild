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

namespace SonarQube.V0_9UpgradeMessageExe
{
    /* The file names of the bootstrapper, pre- and post- processor exes changed
     * between version 0.9 and 1.0 (a breaking change).
     * This exe exists to provide a slightly better user experience for one
     * scenario, namely where the user is running v0.9 of the bootstrapper but
     * has upgraded to a later version of the C# plug-in.
     *
     * The v0.9 bootstrapper will download the zip from the server and then attempt
     * to execute "SonarQube.MSBuild.PreProcessor.exe".
     *
     * This exe (also called "SonarQube.MSBuild.PreProcessor.exe") writes an error
     * message and exits with an error code that will cause the build to fail.
     */

    public static class Program
    {
        static int Main()
        {
            Console.Error.WriteLine(Resources.UpgradeMessage);
            return 1;
        }
    }
}
