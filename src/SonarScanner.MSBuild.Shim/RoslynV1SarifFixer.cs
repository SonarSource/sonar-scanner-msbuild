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

using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SonarScanner.MSBuild.Common;
using SonarScanner.MSBuild.Shim.Interfaces;

namespace SonarScanner.MSBuild.Shim;

public class RoslynV1SarifFixer : IRoslynV1SarifFixer
{
    public /* for test */ const string FixedFileSuffix = "_fixed";

    public const string CSharpLanguage = "cs";
    public const string VBNetLanguage = "vbnet";

    private readonly ILogger logger;

    public RoslynV1SarifFixer(ILogger logger)
    {
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Returns true if the given SARIF came from the VS 2015 RTM Roslyn, which does not provide correct output.
    /// </summary>
    private static bool IsSarifFromRoslynV1(string input, string language)
    {
        // low risk of false positives / false negatives
        if (language.Equals(CSharpLanguage))
        {
            return (input.Contains(@"""toolName"": ""Microsoft (R) Visual C# Compiler""")
                && input.Contains(@"""productVersion"": ""1.0.0"""));
        } else if(language.Equals(VBNetLanguage))
        {
            return (input.Contains(@"""toolName"": ""Microsoft (R) Visual Basic Compiler""")
                && input.Contains(@"""productVersion"": ""1.0.0"""));
        }

        throw new ArgumentException("unknown language: " + language);
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
        var inputLines = unfixedSarif.Split(new string[] { Environment.NewLine }, StringSplitOptions.None);

        /// Example invalid line:
        /// "shortMessage": "message \test\ ["_"]",
        for (var i = 0; i < inputLines.Length; i++)
        {
            var line = inputLines[i];
            if (line.Contains(@"""uri"": ")
                || line.Contains(@"""shortMessage"": ")
                || line.Contains(@"""fullMessage"": ")
                || line.Contains(@"""title"": "))
            {
                line = line.Replace(@"\", @"\\");

                var subStrings = line.Split('"');
                if (subStrings.Length > 5) // expect 5+ substrings because there are 4 syntactically required quotes
                { // any less than 6 substrings and there aren't any quotes to escape
                    var valueStrings = new string[subStrings.Length - 4];
                    Array.Copy(subStrings, 3, valueStrings, 0, subStrings.Length - 4);
                    var newValue = string.Join("\\\"", valueStrings); // join value string together with escaped quotes

                    var newLineStrings = new string[5]
                    {
                            subStrings[0],
                            subStrings[1],
                            subStrings[2],
                            newValue,
                            subStrings[subStrings.Length - 1]
                    }; // construct final line
                    line = string.Join(@"""", newLineStrings); // apply unescaped quotes only where syntactically necessary
                }

                inputLines[i] = line;
            }
        }

        return string.Join(Environment.NewLine, inputLines);
    }

    public string LoadAndFixFile(string sarifFilePath, string language)
    {
        if (!File.Exists(sarifFilePath))
        {
            // file cannot be found -> inherently unfixable
            logger.LogInfo(Resources.MSG_SarifFileNotFound, sarifFilePath);
            return null;
        }

        var inputSarifFileString = File.ReadAllText(sarifFilePath);

        if (IsValidJson(inputSarifFileString))
        {
            // valid input -> no fix required
            logger.LogDebug(Resources.MSG_SarifFileIsValid, sarifFilePath);
            return sarifFilePath;
        }
        logger.LogDebug(Resources.MSG_SarifFileIsInvalid, sarifFilePath);

        if (!IsSarifFromRoslynV1(inputSarifFileString, language))
        {
            // invalid input NOT from Roslyn V1 -> unfixable
            logger.LogWarning(Resources.WARN_SarifFixFail);
            return null;
        }

        var changedSarif = ApplyFixToSarif(inputSarifFileString);

        if (!IsValidJson(changedSarif))
        {
            // output invalid -> unfixable
            logger.LogWarning(Resources.WARN_SarifFixFail);
            return null;
        }
        else
        {
            //output valid -> write to new file and return new path
            var writeDir = Path.GetDirectoryName(sarifFilePath);
            var newSarifFileName =
                Path.GetFileNameWithoutExtension(sarifFilePath) + FixedFileSuffix + Path.GetExtension(sarifFilePath);
            var newSarifFilePath = Path.Combine(writeDir, newSarifFileName);

            File.WriteAllText(newSarifFilePath, changedSarif);

            logger.LogInfo(Resources.MSG_SarifFixSuccess, newSarifFilePath);
            return newSarifFilePath;
        }
    }
}
