//-----------------------------------------------------------------------
// <copyright file="Rule.cs" company="SonarSource SA and Microsoft Corporation">
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
    public class Rule
    {
        public Rule()
        {

        }

        public Rule(string id, string action)
        {
            this.Id = id;
            this.Action = action;
        }

        [XmlAttribute]
        public string Id { get; set; }

        [XmlAttribute]
        public string Action { get; set; }
    }
}
