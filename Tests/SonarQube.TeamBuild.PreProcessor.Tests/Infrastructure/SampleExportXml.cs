//-----------------------------------------------------------------------
// <copyright file="SampleExportXml.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

namespace SonarQube.TeamBuild.PreProcessor.Tests
{
    /// <summary>
    /// Examples of XML strings for testing
    /// </summary>
    internal static class SampleExportXml
    {
        #region Example of XML exported by the Java plugin

        public const string RoslynExportedPluginKey = "csharp";

        public const string RoslynExportedAdditionalFileName = "SonarLint.xml";
        public const string RoslynExportedPackageId = "SonarLint";
        public const string RoslynExportedPackageVersion = "1.6.0";

        public const string RoslynExportedValidSonarLintXml = @"<?xml version=""1.0"" encoding=""utf-8""?>
<RoslynExportProfile Version=""1.0"">
  <Configuration>
    <RuleSet Name=""Rules for SonarQube"" Description=""This rule set was automatically generated from SonarQube."" ToolsVersion=""14.0"">
      <Rules AnalyzerId=""SonarLint.CSharp"" RuleNamespace=""SonarLint.CSharp"">
        <Rule Id=""S3244"" Action=""Warning"" />
        <Rule Id=""S3236"" Action=""Warning"" />
        <Rule Id=""S1121"" Action=""Warning"" />
        <Rule Id=""S3168"" Action=""Warning"" />
        <Rule Id=""S2306"" Action=""Warning"" />
        <Rule Id=""S1764"" Action=""Warning"" />
        <Rule Id=""S1940"" Action=""Warning"" />
        <Rule Id=""S1125"" Action=""Warning"" />
        <Rule Id=""S3215"" Action=""Warning"" />
        <Rule Id=""S2486"" Action=""Warning"" />
        <Rule Id=""S2737"" Action=""Warning"" />
        <Rule Id=""S1118"" Action=""Warning"" />
        <Rule Id=""S1155"" Action=""Warning"" />
        <Rule Id=""S2971"" Action=""Warning"" />
        <Rule Id=""S3216"" Action=""Warning"" />
        <Rule Id=""S125"" Action=""Warning"" />
        <Rule Id=""S1134"" Action=""Warning"" />
        <Rule Id=""S1135"" Action=""Warning"" />
        <Rule Id=""S3240"" Action=""Warning"" />
        <Rule Id=""S1862"" Action=""Warning"" />
        <Rule Id=""S1871"" Action=""Warning"" />
        <Rule Id=""S2228"" Action=""Warning"" />
        <Rule Id=""S1699"" Action=""Warning"" />
        <Rule Id=""S1854"" Action=""Warning"" />
        <Rule Id=""S3253"" Action=""Warning"" />
        <Rule Id=""S3172"" Action=""Warning"" />
        <Rule Id=""S2931"" Action=""Warning"" />
        <Rule Id=""S2930"" Action=""Warning"" />
        <Rule Id=""S2997"" Action=""Warning"" />
        <Rule Id=""S2952"" Action=""Warning"" />
        <Rule Id=""S2953"" Action=""Warning"" />
        <Rule Id=""S1186"" Action=""Warning"" />
        <Rule Id=""S108"" Action=""Warning"" />
        <Rule Id=""S2291"" Action=""Warning"" />
        <Rule Id=""S1116"" Action=""Warning"" />
        <Rule Id=""S2344"" Action=""Warning"" />
        <Rule Id=""S1244"" Action=""Warning"" />
        <Rule Id=""S1067"" Action=""Warning"" />
        <Rule Id=""S3052"" Action=""Warning"" />
        <Rule Id=""S2387"" Action=""Warning"" />
        <Rule Id=""S2933"" Action=""Warning"" />
        <Rule Id=""S2345"" Action=""Warning"" />
        <Rule Id=""S2346"" Action=""Warning"" />
        <Rule Id=""S3376"" Action=""Warning"" />
        <Rule Id=""S3217"" Action=""Warning"" />
        <Rule Id=""S127"" Action=""Warning"" />
        <Rule Id=""S1994"" Action=""Warning"" />
        <Rule Id=""S1541"" Action=""Warning"" />
        <Rule Id=""S2934"" Action=""Warning"" />
        <Rule Id=""S2955"" Action=""Warning"" />
        <Rule Id=""S3246"" Action=""Warning"" />
        <Rule Id=""S2326"" Action=""Warning"" />
        <Rule Id=""S3397"" Action=""Warning"" />
        <Rule Id=""S3249"" Action=""Warning"" />
        <Rule Id=""S2328"" Action=""Warning"" />
        <Rule Id=""S2219"" Action=""Warning"" />
        <Rule Id=""S907"" Action=""Warning"" />
        <Rule Id=""S1313"" Action=""Warning"" />
        <Rule Id=""S1066"" Action=""Warning"" />
        <Rule Id=""S1145"" Action=""Warning"" />
        <Rule Id=""S2692"" Action=""Warning"" />
        <Rule Id=""S818"" Action=""Warning"" />
        <Rule Id=""S2551"" Action=""Warning"" />
        <Rule Id=""S3218"" Action=""Warning"" />
        <Rule Id=""S3427"" Action=""Warning"" />
        <Rule Id=""S3262"" Action=""Warning"" />
        <Rule Id=""S1172"" Action=""Warning"" />
        <Rule Id=""S2681"" Action=""Warning"" />
        <Rule Id=""S1659"" Action=""Warning"" />
        <Rule Id=""S1848"" Action=""Warning"" />
        <Rule Id=""S2360"" Action=""Warning"" />
        <Rule Id=""S3220"" Action=""Warning"" />
        <Rule Id=""S3169"" Action=""Warning"" />
        <Rule Id=""S1226"" Action=""Warning"" />
        <Rule Id=""S927"" Action=""Warning"" />
        <Rule Id=""S2234"" Action=""Warning"" />
        <Rule Id=""S2372"" Action=""Warning"" />
        <Rule Id=""S2292"" Action=""Warning"" />
        <Rule Id=""S2376"" Action=""Warning"" />
        <Rule Id=""S2368"" Action=""Warning"" />
        <Rule Id=""S3254"" Action=""Warning"" />
        <Rule Id=""S1905"" Action=""Warning"" />
        <Rule Id=""S1939"" Action=""Warning"" />
        <Rule Id=""S2333"" Action=""Warning"" />
        <Rule Id=""S3235"" Action=""Warning"" />
        <Rule Id=""S2995"" Action=""Warning"" />
        <Rule Id=""S2757"" Action=""Warning"" />
        <Rule Id=""S1656"" Action=""Warning"" />
        <Rule Id=""S1697"" Action=""Warning"" />
        <Rule Id=""S2437"" Action=""Warning"" />
        <Rule Id=""S122"" Action=""Warning"" />
        <Rule Id=""S2674"" Action=""Warning"" />
        <Rule Id=""S2743"" Action=""Warning"" />
        <Rule Id=""S3263"" Action=""Warning"" />
        <Rule Id=""S2223"" Action=""Warning"" />
        <Rule Id=""S2696"" Action=""Warning"" />
        <Rule Id=""S1643"" Action=""Warning"" />
        <Rule Id=""S1449"" Action=""Warning"" />
        <Rule Id=""S2275"" Action=""Warning"" />
        <Rule Id=""S3234"" Action=""Warning"" />
        <Rule Id=""S131"" Action=""Warning"" />
        <Rule Id=""S2758"" Action=""Warning"" />
        <Rule Id=""S3005"" Action=""Warning"" />
        <Rule Id=""S2996"" Action=""Warning"" />
        <Rule Id=""S1479"" Action=""Warning"" />
        <Rule Id=""S107"" Action=""Warning"" />
        <Rule Id=""S2225"" Action=""Warning"" />
        <Rule Id=""S2761"" Action=""Warning"" />
        <Rule Id=""S3237"" Action=""Warning"" />
        <Rule Id=""S2123"" Action=""Warning"" />
        <Rule Id=""S1117"" Action=""Warning"" />
        <Rule Id=""S1481"" Action=""Warning"" />
        <Rule Id=""S126"" Action=""Warning"" />
        <Rule Id=""S2760"" Action=""Warning"" />
        <Rule Id=""S1227"" Action=""Warning"" />
        <Rule Id=""S1109"" Action=""Warning"" />
        <Rule Id=""S2278"" Action=""Warning"" />
        <Rule Id=""S1694"" Action=""Warning"" />
        <Rule Id=""S2339"" Action=""Warning"" />
        <Rule Id=""S2330"" Action=""Warning"" />
        <Rule Id=""S100"" Action=""Warning"" />
        <Rule Id=""S101"" Action=""Warning"" />
        <Rule Id=""S2070"" Action=""Warning"" />
        <Rule Id=""S2197"" Action=""Warning"" />
        <Rule Id=""S121"" Action=""Warning"" />
        <Rule Id=""S1301"" Action=""Warning"" />
        <Rule Id=""S2357"" Action=""Warning"" />
        <Rule Id=""S104"" Action=""Warning"" />
        <Rule Id=""S105"" Action=""Warning"" />
        <Rule Id=""S2290"" Action=""Warning"" />
        <Rule Id=""S103"" Action=""Warning"" />
      </Rules>
    </RuleSet>
    <AdditionalFiles>
      <AdditionalFile FileName=""SonarLint.xml"">PD94bWwgdmVyc2lvbj0iMS4wIiBlbmNvZGluZz0iVVRGLTgiPz4NCjxBbmFseXNpc0lucHV0Pg0KICA8UnVsZXM+DQogICAgPFJ1bGU+DQogICAgICA8S2V5PlMzMjQ0PC9LZXk+DQogICAgPC9SdWxlPg0KICAgIDxSdWxlPg0KICAgICAgPEtleT5TMzIzNjwvS2V5Pg0KICAgIDwvUnVsZT4NCiAgICA8UnVsZT4NCiAgICAgIDxLZXk+UzExMjE8L0tleT4NCiAgICA8L1J1bGU+DQogICAgPFJ1bGU+DQogICAgICA8S2V5PlMzMTY4PC9LZXk+DQogICAgPC9SdWxlPg0KICAgIDxSdWxlPg0KICAgICAgPEtleT5TMjMwNjwvS2V5Pg0KICAgIDwvUnVsZT4NCiAgICA8UnVsZT4NCiAgICAgIDxLZXk+UzE3NjQ8L0tleT4NCiAgICA8L1J1bGU+DQogICAgPFJ1bGU+DQogICAgICA8S2V5PlMxOTQwPC9LZXk+DQogICAgPC9SdWxlPg0KICAgIDxSdWxlPg0KICAgICAgPEtleT5TMTEyNTwvS2V5Pg0KICAgIDwvUnVsZT4NCiAgICA8UnVsZT4NCiAgICAgIDxLZXk+UzMyMTU8L0tleT4NCiAgICA8L1J1bGU+DQogICAgPFJ1bGU+DQogICAgICA8S2V5PlMyNDg2PC9LZXk+DQogICAgPC9SdWxlPg0KICAgIDxSdWxlPg0KICAgICAgPEtleT5TMjczNzwvS2V5Pg0KICAgIDwvUnVsZT4NCiAgICA8UnVsZT4NCiAgICAgIDxLZXk+UzExMTg8L0tleT4NCiAgICA8L1J1bGU+DQogICAgPFJ1bGU+DQogICAgICA8S2V5PlMxMTU1PC9LZXk+DQogICAgPC9SdWxlPg0KICAgIDxSdWxlPg0KICAgICAgPEtleT5TMjk3MTwvS2V5Pg0KICAgIDwvUnVsZT4NCiAgICA8UnVsZT4NCiAgICAgIDxLZXk+UzMyMTY8L0tleT4NCiAgICA8L1J1bGU+DQogICAgPFJ1bGU+DQogICAgICA8S2V5PlMxMjU8L0tleT4NCiAgICA8L1J1bGU+DQogICAgPFJ1bGU+DQogICAgICA8S2V5PlMxMTM0PC9LZXk+DQogICAgPC9SdWxlPg0KICAgIDxSdWxlPg0KICAgICAgPEtleT5TMTEzNTwvS2V5Pg0KICAgIDwvUnVsZT4NCiAgICA8UnVsZT4NCiAgICAgIDxLZXk+UzMyNDA8L0tleT4NCiAgICA8L1J1bGU+DQogICAgPFJ1bGU+DQogICAgICA8S2V5PlMxODYyPC9LZXk+DQogICAgPC9SdWxlPg0KICAgIDxSdWxlPg0KICAgICAgPEtleT5TMTg3MTwvS2V5Pg0KICAgIDwvUnVsZT4NCiAgICA8UnVsZT4NCiAgICAgIDxLZXk+UzIyMjg8L0tleT4NCiAgICA8L1J1bGU+DQogICAgPFJ1bGU+DQogICAgICA8S2V5PlMxNjk5PC9LZXk+DQogICAgPC9SdWxlPg0KICAgIDxSdWxlPg0KICAgICAgPEtleT5TMTg1NDwvS2V5Pg0KICAgIDwvUnVsZT4NCiAgICA8UnVsZT4NCiAgICAgIDxLZXk+UzMyNTM8L0tleT4NCiAgICA8L1J1bGU+DQogICAgPFJ1bGU+DQogICAgICA8S2V5PlMzMTcyPC9LZXk+DQogICAgPC9SdWxlPg0KICAgIDxSdWxlPg0KICAgICAgPEtleT5TMjkzMTwvS2V5Pg0KICAgIDwvUnVsZT4NCiAgICA8UnVsZT4NCiAgICAgIDxLZXk+UzI5MzA8L0tleT4NCiAgICA8L1J1bGU+DQogICAgPFJ1bGU+DQogICAgICA8S2V5PlMyOTk3PC9LZXk+DQogICAgPC9SdWxlPg0KICAgIDxSdWxlPg0KICAgICAgPEtleT5TMjk1MjwvS2V5Pg0KICAgIDwvUnVsZT4NCiAgICA8UnVsZT4NCiAgICAgIDxLZXk+UzI5NTM8L0tleT4NCiAgICA8L1J1bGU+DQogICAgPFJ1bGU+DQogICAgICA8S2V5PlMxMTg2PC9LZXk+DQogICAgPC9SdWxlPg0KICAgIDxSdWxlPg0KICAgICAgPEtleT5TMTA4PC9LZXk+DQogICAgPC9SdWxlPg0KICAgIDxSdWxlPg0KICAgICAgPEtleT5TMjI5MTwvS2V5Pg0KICAgIDwvUnVsZT4NCiAgICA8UnVsZT4NCiAgICAgIDxLZXk+UzExMTY8L0tleT4NCiAgICA8L1J1bGU+DQogICAgPFJ1bGU+DQogICAgICA8S2V5PlMyMzQ0PC9LZXk+DQogICAgPC9SdWxlPg0KICAgIDxSdWxlPg0KICAgICAgPEtleT5TMTI0NDwvS2V5Pg0KICAgIDwvUnVsZT4NCiAgICA8UnVsZT4NCiAgICAgIDxLZXk+UzEwNjc8L0tleT4NCiAgICAgIDxQYXJhbWV0ZXJzPg0KICAgICAgICA8UGFyYW1ldGVyPg0KICAgICAgICAgIDxLZXk+bWF4PC9LZXk+DQogICAgICAgICAgPFZhbHVlPjM8L1ZhbHVlPg0KICAgICAgICA8L1BhcmFtZXRlcj4NCiAgICAgIDwvUGFyYW1ldGVycz4NCiAgICA8L1J1bGU+DQogICAgPFJ1bGU+DQogICAgICA8S2V5PlMzMDUyPC9LZXk+DQogICAgPC9SdWxlPg0KICAgIDxSdWxlPg0KICAgICAgPEtleT5TMjM4NzwvS2V5Pg0KICAgIDwvUnVsZT4NCiAgICA8UnVsZT4NCiAgICAgIDxLZXk+UzI5MzM8L0tleT4NCiAgICA8L1J1bGU+DQogICAgPFJ1bGU+DQogICAgICA8S2V5PlMyMzQ1PC9LZXk+DQogICAgPC9SdWxlPg0KICAgIDxSdWxlPg0KICAgICAgPEtleT5TMjM0NjwvS2V5Pg0KICAgIDwvUnVsZT4NCiAgICA8UnVsZT4NCiAgICAgIDxLZXk+UzMzNzY8L0tleT4NCiAgICA8L1J1bGU+DQogICAgPFJ1bGU+DQogICAgICA8S2V5PlMzMjE3PC9LZXk+DQogICAgPC9SdWxlPg0KICAgIDxSdWxlPg0KICAgICAgPEtleT5TMTI3PC9LZXk+DQogICAgPC9SdWxlPg0KICAgIDxSdWxlPg0KICAgICAgPEtleT5TMTk5NDwvS2V5Pg0KICAgIDwvUnVsZT4NCiAgICA8UnVsZT4NCiAgICAgIDxLZXk+UzE1NDE8L0tleT4NCiAgICAgIDxQYXJhbWV0ZXJzPg0KICAgICAgICA8UGFyYW1ldGVyPg0KICAgICAgICAgIDxLZXk+bWF4aW11bUZ1bmN0aW9uQ29tcGxleGl0eVRocmVzaG9sZDwvS2V5Pg0KICAgICAgICAgIDxWYWx1ZT4xMDwvVmFsdWU+DQogICAgICAgIDwvUGFyYW1ldGVyPg0KICAgICAgPC9QYXJhbWV0ZXJzPg0KICAgIDwvUnVsZT4NCiAgICA8UnVsZT4NCiAgICAgIDxLZXk+UzI5MzQ8L0tleT4NCiAgICA8L1J1bGU+DQogICAgPFJ1bGU+DQogICAgICA8S2V5PlMyOTU1PC9LZXk+DQogICAgPC9SdWxlPg0KICAgIDxSdWxlPg0KICAgICAgPEtleT5TMzI0NjwvS2V5Pg0KICAgIDwvUnVsZT4NCiAgICA8UnVsZT4NCiAgICAgIDxLZXk+UzIzMjY8L0tleT4NCiAgICA8L1J1bGU+DQogICAgPFJ1bGU+DQogICAgICA8S2V5PlMzMzk3PC9LZXk+DQogICAgPC9SdWxlPg0KICAgIDxSdWxlPg0KICAgICAgPEtleT5TMzI0OTwvS2V5Pg0KICAgIDwvUnVsZT4NCiAgICA8UnVsZT4NCiAgICAgIDxLZXk+UzIzMjg8L0tleT4NCiAgICA8L1J1bGU+DQogICAgPFJ1bGU+DQogICAgICA8S2V5PlMyMjE5PC9LZXk+DQogICAgPC9SdWxlPg0KICAgIDxSdWxlPg0KICAgICAgPEtleT5TOTA3PC9LZXk+DQogICAgPC9SdWxlPg0KICAgIDxSdWxlPg0KICAgICAgPEtleT5TMTMxMzwvS2V5Pg0KICAgIDwvUnVsZT4NCiAgICA8UnVsZT4NCiAgICAgIDxLZXk+UzEwNjY8L0tleT4NCiAgICA8L1J1bGU+DQogICAgPFJ1bGU+DQogICAgICA8S2V5PlMxMTQ1PC9LZXk+DQogICAgPC9SdWxlPg0KICAgIDxSdWxlPg0KICAgICAgPEtleT5TMjY5MjwvS2V5Pg0KICAgIDwvUnVsZT4NCiAgICA8UnVsZT4NCiAgICAgIDxLZXk+UzgxODwvS2V5Pg0KICAgIDwvUnVsZT4NCiAgICA8UnVsZT4NCiAgICAgIDxLZXk+UzI1NTE8L0tleT4NCiAgICA8L1J1bGU+DQogICAgPFJ1bGU+DQogICAgICA8S2V5PlMzMjE4PC9LZXk+DQogICAgPC9SdWxlPg0KICAgIDxSdWxlPg0KICAgICAgPEtleT5TMzQyNzwvS2V5Pg0KICAgIDwvUnVsZT4NCiAgICA8UnVsZT4NCiAgICAgIDxLZXk+UzMyNjI8L0tleT4NCiAgICA8L1J1bGU+DQogICAgPFJ1bGU+DQogICAgICA8S2V5PlMxMTcyPC9LZXk+DQogICAgPC9SdWxlPg0KICAgIDxSdWxlPg0KICAgICAgPEtleT5TMjY4MTwvS2V5Pg0KICAgIDwvUnVsZT4NCiAgICA8UnVsZT4NCiAgICAgIDxLZXk+UzE2NTk8L0tleT4NCiAgICA8L1J1bGU+DQogICAgPFJ1bGU+DQogICAgICA8S2V5PlMxODQ4PC9LZXk+DQogICAgPC9SdWxlPg0KICAgIDxSdWxlPg0KICAgICAgPEtleT5TMjM2MDwvS2V5Pg0KICAgIDwvUnVsZT4NCiAgICA8UnVsZT4NCiAgICAgIDxLZXk+UzMyMjA8L0tleT4NCiAgICA8L1J1bGU+DQogICAgPFJ1bGU+DQogICAgICA8S2V5PlMzMTY5PC9LZXk+DQogICAgPC9SdWxlPg0KICAgIDxSdWxlPg0KICAgICAgPEtleT5TMTIyNjwvS2V5Pg0KICAgIDwvUnVsZT4NCiAgICA8UnVsZT4NCiAgICAgIDxLZXk+UzkyNzwvS2V5Pg0KICAgIDwvUnVsZT4NCiAgICA8UnVsZT4NCiAgICAgIDxLZXk+UzIyMzQ8L0tleT4NCiAgICA8L1J1bGU+DQogICAgPFJ1bGU+DQogICAgICA8S2V5PlMyMzcyPC9LZXk+DQogICAgPC9SdWxlPg0KICAgIDxSdWxlPg0KICAgICAgPEtleT5TMjI5MjwvS2V5Pg0KICAgIDwvUnVsZT4NCiAgICA8UnVsZT4NCiAgICAgIDxLZXk+UzIzNzY8L0tleT4NCiAgICA8L1J1bGU+DQogICAgPFJ1bGU+DQogICAgICA8S2V5PlMyMzY4PC9LZXk+DQogICAgPC9SdWxlPg0KICAgIDxSdWxlPg0KICAgICAgPEtleT5TMzI1NDwvS2V5Pg0KICAgIDwvUnVsZT4NCiAgICA8UnVsZT4NCiAgICAgIDxLZXk+UzE5MDU8L0tleT4NCiAgICA8L1J1bGU+DQogICAgPFJ1bGU+DQogICAgICA8S2V5PlMxOTM5PC9LZXk+DQogICAgPC9SdWxlPg0KICAgIDxSdWxlPg0KICAgICAgPEtleT5TMjMzMzwvS2V5Pg0KICAgIDwvUnVsZT4NCiAgICA8UnVsZT4NCiAgICAgIDxLZXk+UzMyMzU8L0tleT4NCiAgICA8L1J1bGU+DQogICAgPFJ1bGU+DQogICAgICA8S2V5PlMyOTk1PC9LZXk+DQogICAgPC9SdWxlPg0KICAgIDxSdWxlPg0KICAgICAgPEtleT5TMjc1NzwvS2V5Pg0KICAgIDwvUnVsZT4NCiAgICA8UnVsZT4NCiAgICAgIDxLZXk+UzE2NTY8L0tleT4NCiAgICA8L1J1bGU+DQogICAgPFJ1bGU+DQogICAgICA8S2V5PlMxNjk3PC9LZXk+DQogICAgPC9SdWxlPg0KICAgIDxSdWxlPg0KICAgICAgPEtleT5TMjQzNzwvS2V5Pg0KICAgIDwvUnVsZT4NCiAgICA8UnVsZT4NCiAgICAgIDxLZXk+UzEyMjwvS2V5Pg0KICAgIDwvUnVsZT4NCiAgICA8UnVsZT4NCiAgICAgIDxLZXk+UzI2NzQ8L0tleT4NCiAgICA8L1J1bGU+DQogICAgPFJ1bGU+DQogICAgICA8S2V5PlMyNzQzPC9LZXk+DQogICAgPC9SdWxlPg0KICAgIDxSdWxlPg0KICAgICAgPEtleT5TMzI2MzwvS2V5Pg0KICAgIDwvUnVsZT4NCiAgICA8UnVsZT4NCiAgICAgIDxLZXk+UzIyMjM8L0tleT4NCiAgICA8L1J1bGU+DQogICAgPFJ1bGU+DQogICAgICA8S2V5PlMyNjk2PC9LZXk+DQogICAgPC9SdWxlPg0KICAgIDxSdWxlPg0KICAgICAgPEtleT5TMTY0MzwvS2V5Pg0KICAgIDwvUnVsZT4NCiAgICA8UnVsZT4NCiAgICAgIDxLZXk+UzE0NDk8L0tleT4NCiAgICA8L1J1bGU+DQogICAgPFJ1bGU+DQogICAgICA8S2V5PlMyMjc1PC9LZXk+DQogICAgPC9SdWxlPg0KICAgIDxSdWxlPg0KICAgICAgPEtleT5TMzIzNDwvS2V5Pg0KICAgIDwvUnVsZT4NCiAgICA8UnVsZT4NCiAgICAgIDxLZXk+UzEzMTwvS2V5Pg0KICAgIDwvUnVsZT4NCiAgICA8UnVsZT4NCiAgICAgIDxLZXk+UzI3NTg8L0tleT4NCiAgICA8L1J1bGU+DQogICAgPFJ1bGU+DQogICAgICA8S2V5PlMzMDA1PC9LZXk+DQogICAgPC9SdWxlPg0KICAgIDxSdWxlPg0KICAgICAgPEtleT5TMjk5NjwvS2V5Pg0KICAgIDwvUnVsZT4NCiAgICA8UnVsZT4NCiAgICAgIDxLZXk+UzE0Nzk8L0tleT4NCiAgICAgIDxQYXJhbWV0ZXJzPg0KICAgICAgICA8UGFyYW1ldGVyPg0KICAgICAgICAgIDxLZXk+bWF4aW11bTwvS2V5Pg0KICAgICAgICAgIDxWYWx1ZT4zMDwvVmFsdWU+DQogICAgICAgIDwvUGFyYW1ldGVyPg0KICAgICAgPC9QYXJhbWV0ZXJzPg0KICAgIDwvUnVsZT4NCiAgICA8UnVsZT4NCiAgICAgIDxLZXk+UzEwNzwvS2V5Pg0KICAgICAgPFBhcmFtZXRlcnM+DQogICAgICAgIDxQYXJhbWV0ZXI+DQogICAgICAgICAgPEtleT5tYXg8L0tleT4NCiAgICAgICAgICA8VmFsdWU+NzwvVmFsdWU+DQogICAgICAgIDwvUGFyYW1ldGVyPg0KICAgICAgPC9QYXJhbWV0ZXJzPg0KICAgIDwvUnVsZT4NCiAgICA8UnVsZT4NCiAgICAgIDxLZXk+UzIyMjU8L0tleT4NCiAgICA8L1J1bGU+DQogICAgPFJ1bGU+DQogICAgICA8S2V5PlMyNzYxPC9LZXk+DQogICAgPC9SdWxlPg0KICAgIDxSdWxlPg0KICAgICAgPEtleT5TMzIzNzwvS2V5Pg0KICAgIDwvUnVsZT4NCiAgICA8UnVsZT4NCiAgICAgIDxLZXk+UzIxMjM8L0tleT4NCiAgICA8L1J1bGU+DQogICAgPFJ1bGU+DQogICAgICA8S2V5PlMxMTE3PC9LZXk+DQogICAgPC9SdWxlPg0KICAgIDxSdWxlPg0KICAgICAgPEtleT5TMTQ4MTwvS2V5Pg0KICAgIDwvUnVsZT4NCiAgICA8UnVsZT4NCiAgICAgIDxLZXk+UzEyNjwvS2V5Pg0KICAgIDwvUnVsZT4NCiAgICA8UnVsZT4NCiAgICAgIDxLZXk+UzI3NjA8L0tleT4NCiAgICA8L1J1bGU+DQogICAgPFJ1bGU+DQogICAgICA8S2V5PlMxMjI3PC9LZXk+DQogICAgPC9SdWxlPg0KICAgIDxSdWxlPg0KICAgICAgPEtleT5TMTEwOTwvS2V5Pg0KICAgIDwvUnVsZT4NCiAgICA8UnVsZT4NCiAgICAgIDxLZXk+UzIyNzg8L0tleT4NCiAgICA8L1J1bGU+DQogICAgPFJ1bGU+DQogICAgICA8S2V5PlMxNjk0PC9LZXk+DQogICAgPC9SdWxlPg0KICAgIDxSdWxlPg0KICAgICAgPEtleT5TMjMzOTwvS2V5Pg0KICAgIDwvUnVsZT4NCiAgICA8UnVsZT4NCiAgICAgIDxLZXk+UzIzMzA8L0tleT4NCiAgICA8L1J1bGU+DQogICAgPFJ1bGU+DQogICAgICA8S2V5PlMxMDA8L0tleT4NCiAgICAgIDxQYXJhbWV0ZXJzPg0KICAgICAgICA8UGFyYW1ldGVyPg0KICAgICAgICAgIDxLZXk+Zm9ybWF0PC9LZXk+DQogICAgICAgICAgPFZhbHVlPl5bQS1aXVthLXpBLVowLTlfXSpbYS16QS1aMC05XSQ8L1ZhbHVlPg0KICAgICAgICA8L1BhcmFtZXRlcj4NCiAgICAgIDwvUGFyYW1ldGVycz4NCiAgICA8L1J1bGU+DQogICAgPFJ1bGU+DQogICAgICA8S2V5PlMxMDE8L0tleT4NCiAgICAgIDxQYXJhbWV0ZXJzPg0KICAgICAgICA8UGFyYW1ldGVyPg0KICAgICAgICAgIDxLZXk+Zm9ybWF0PC9LZXk+DQogICAgICAgICAgPFZhbHVlPl4oW0EtSEotWl1bYS16QS1aMC05XSt8SVthLXowLTldW2EtekEtWjAtOV0qfFtBLVpdW2EtekEtWjAtOV0rRXh0ZW5zaW9ucykkPC9WYWx1ZT4NCiAgICAgICAgPC9QYXJhbWV0ZXI+DQogICAgICA8L1BhcmFtZXRlcnM+DQogICAgPC9SdWxlPg0KICAgIDxSdWxlPg0KICAgICAgPEtleT5TMjA3MDwvS2V5Pg0KICAgIDwvUnVsZT4NCiAgICA8UnVsZT4NCiAgICAgIDxLZXk+UzIxOTc8L0tleT4NCiAgICA8L1J1bGU+DQogICAgPFJ1bGU+DQogICAgICA8S2V5PlMxMjE8L0tleT4NCiAgICA8L1J1bGU+DQogICAgPFJ1bGU+DQogICAgICA8S2V5PlMxMzAxPC9LZXk+DQogICAgPC9SdWxlPg0KICAgIDxSdWxlPg0KICAgICAgPEtleT5TMjM1NzwvS2V5Pg0KICAgIDwvUnVsZT4NCiAgICA8UnVsZT4NCiAgICAgIDxLZXk+UzEwNDwvS2V5Pg0KICAgICAgPFBhcmFtZXRlcnM+DQogICAgICAgIDxQYXJhbWV0ZXI+DQogICAgICAgICAgPEtleT5tYXhpbXVtRmlsZUxvY1RocmVzaG9sZDwvS2V5Pg0KICAgICAgICAgIDxWYWx1ZT4xMDAwPC9WYWx1ZT4NCiAgICAgICAgPC9QYXJhbWV0ZXI+DQogICAgICA8L1BhcmFtZXRlcnM+DQogICAgPC9SdWxlPg0KICAgIDxSdWxlPg0KICAgICAgPEtleT5TMTA1PC9LZXk+DQogICAgPC9SdWxlPg0KICAgIDxSdWxlPg0KICAgICAgPEtleT5TMjI5MDwvS2V5Pg0KICAgIDwvUnVsZT4NCiAgICA8UnVsZT4NCiAgICAgIDxLZXk+UzEwMzwvS2V5Pg0KICAgICAgPFBhcmFtZXRlcnM+DQogICAgICAgIDxQYXJhbWV0ZXI+DQogICAgICAgICAgPEtleT5tYXhpbXVtTGluZUxlbmd0aDwvS2V5Pg0KICAgICAgICAgIDxWYWx1ZT4yMDA8L1ZhbHVlPg0KICAgICAgICA8L1BhcmFtZXRlcj4NCiAgICAgIDwvUGFyYW1ldGVycz4NCiAgICA8L1J1bGU+DQogIDwvUnVsZXM+DQogIDxGaWxlcz4NCiAgPC9GaWxlcz4NCjwvQW5hbHlzaXNJbnB1dD4NCg==</AdditionalFile>
    </AdditionalFiles>
  </Configuration>
  <Deployment>
    <NuGetPackages>
      <NuGetPackage Id=""SonarLint"" Version=""1.6.0"" />
    </NuGetPackages>
  </Deployment>
</RoslynExportProfile>
";
        #endregion

    }
}
