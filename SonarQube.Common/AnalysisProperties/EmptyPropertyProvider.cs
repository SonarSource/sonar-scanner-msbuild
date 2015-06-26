//-----------------------------------------------------------------------
// <copyright file="EmptyPropertyProvider.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using System.Collections.Generic;
using System.Linq;

namespace SonarQube.Common
{
    public class EmptyPropertyProvider : IAnalysisPropertyProvider
    {
        public static readonly IAnalysisPropertyProvider Instance = new EmptyPropertyProvider();

        private EmptyPropertyProvider()
        {
        }

        #region IAnalysisPropertyProvider interface

        public IEnumerable<Property> GetAllProperties()
        {
            return Enumerable.Empty<Property>();
        }

        public bool TryGetProperty(string key, out Property property)
        {
            property = null;
            return false;
        }

        #endregion
    }
}
