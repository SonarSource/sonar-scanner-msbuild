//-----------------------------------------------------------------------
// <copyright file="RoslynV1SarifFixer.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SonarQube.Common;
using System;
using System.IO;

namespace SonarRunner.Shim
{
    public class RoslynV1SarifFixer : IRoslynV1SarifFixer
    {

        public const string ReportFilePropertyKey = "sonar.cs.roslyn.reportFilePath";

        public /* for test */ const string FixedFileSuffix = "_fixed";

        #region Private Methods

        /// <summary>
        /// Returns true if the given SARIF came from the VS 2015 RTM Roslyn, which does not provide correct output.
        /// </summary>
        private static bool IsSarifFromRoslynV1(string input)
        {
            // low risk of false positives / false negatives
            return (input.Contains(@"""toolName"": ""Microsoft (R) Visual C# Compiler""")
                && input.Contains(@"""productVersion"": ""1.0.0"""));
        }

        /// <summary>
        /// Returns true if the input is parseable JSON. No checks are made for conformation to the SARIF specification.
        /// </summary>
        private static bool IsValidJson(string input)
        {
            try
            {
                JObject.Parse(input);     
            }
            catch (JsonReaderException) // we expect invalid JSON
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// The low-level implementation of the the fix - applying escaping to backslashes and quotes.
        /// </summary>
        private static string ApplyFixToSarif(string unfixedSarif)
        {
            string[] inputLines = unfixedSarif.Split(new string[] { Environment.NewLine }, StringSplitOptions.None);

            /// Example invalid line:
            /// "shortMessage": "message \test\ ["_"]",
            for (int i = 0; i < inputLines.Length; i++)
            {
                string line = inputLines[i];
                if (line.Contains(@"""uri"": ")
                    || line.Contains(@"""shortMessage"": ")
                    || line.Contains(@"""fullMessage"": ")
                    || line.Contains(@"""title"": "))
                {
                    line = line.Replace(@"\", @"\\");

                    string[] subStrings = line.Split('"');
                    if (subStrings.Length > 5) // expect 5+ substrings because there are 4 syntactically required quotes
                    { // any less than 6 substrings and there aren't any quotes to escape
                        string[] valueStrings = new string[subStrings.Length - 4];
                        Array.Copy(subStrings, 3, valueStrings, 0, subStrings.Length - 4);
                        string newValue = String.Join("\\\"", valueStrings); // join value string together with escaped quotes

                        string[] newLineStrings = new string[5]
                        {
                                subStrings[0],
                                subStrings[1],
                                subStrings[2],
                                newValue,
                                subStrings[subStrings.Length - 1]
                        }; // construct final line
                        line = String.Join(@"""", newLineStrings); // apply unescaped quotes only where syntactically necessary
                    }

                    inputLines[i] = line;
                }
            }

            return string.Join(Environment.NewLine, inputLines);
        }

        #endregion

        #region IRoslynV1SarifFixer

        public string LoadAndFixFile(string sarifFilePath, ILogger logger)
        {
            if (!File.Exists(sarifFilePath))
            {
                // file cannot be found -> inherently unfixable
                logger.LogInfo(Resources.MSG_SarifFileNotFound, sarifFilePath);
                return null;
            }

            string inputSarifFileString = File.ReadAllText(sarifFilePath);

            if (IsValidJson(inputSarifFileString))
            {
                // valid input -> no fix required
                logger.LogDebug(Resources.MSG_SarifFileIsValid, sarifFilePath);
                return sarifFilePath;
            }
            logger.LogDebug(Resources.MSG_SarifFileIsInvalid, sarifFilePath);

            if (!IsSarifFromRoslynV1(inputSarifFileString))
            {
                // invalid input NOT from Roslyn V1 -> unfixable
                logger.LogWarning(Resources.WARN_SarifFixFail);
                return null;
            }
                
            string changedSarif = ApplyFixToSarif(inputSarifFileString);

            if (!IsValidJson(changedSarif))
            {
                // output invalid -> unfixable
                logger.LogWarning(Resources.WARN_SarifFixFail);
                return null;
            }
            else
            {
                //output valid -> write to new file and return new path
                string writeDir = Path.GetDirectoryName(sarifFilePath);
                string newSarifFileName =
                    Path.GetFileNameWithoutExtension(sarifFilePath) + FixedFileSuffix + Path.GetExtension(sarifFilePath);
                string newSarifFilePath = Path.Combine(writeDir, newSarifFileName);

                File.WriteAllText(newSarifFilePath, changedSarif);

                logger.LogInfo(Resources.MSG_SarifFixSuccess, newSarifFilePath);
                return newSarifFilePath;
            }
        }

    #endregion

    }
}
