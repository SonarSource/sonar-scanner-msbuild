//-----------------------------------------------------------------------
// <copyright file="Rules.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace SonarQube.Common
{
    public class Rules
    {
        [XmlAttribute]
        public string AnalyzerId { get; set; }

        [XmlAttribute]
        public string RuleNamespace { get; set; }

        [XmlElement("Rule")]
        public List<Rule> RuleList { get; set; }
    }
}
