//-----------------------------------------------------------------------
// <copyright file="Foo.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using System;

namespace ProjectUnderTest
{
    /* FxCop violations: 
    *
    * CA1801 (unused params) -- not in quality profile
    * CA1822 (Bar can be static) -- not in quality profile
    * CA2201 (Do not raise reserved exceptions)
    * CA1303 (Do not pass literals as localized params)

    * SonarLint violations
    *   S2228 (do not use Console)
    *   S1134 (no 'F i x m e' comments)
    *   S1135 (no 'T O D O' comments)  -- not in quality profile
    *   S107 (method should not have to many parameters) -- not in quality profile, and only triggered with the right parameters
    *
    * Also triggered: 
    *   common-cs:InsufficientCommentDensity -- not in quality profile
    *    
    */

    public class Foo
    {
        public string Bar
        {
            get
            {
                // violates S2228 
                Console.WriteLine("Hello world");

                // violates S1135
                //TODO: lorem ipsum 

                // violates S1134 
                return String.Empty; //FIXME please
            }
            set
            {
                // Violates FxCop's CA2201: Do not raise reserved exception types (major issue)
                throw new Exception("Hello world");
            }
        }
        
        public int test(int i1, int i2, int i3)
        {
        	return i1 + i2 + i3;
        }
    }
}
