using System;

namespace ProjectUnderTest
{
    /* FxCop violations: 
    *
    * CA1801 (unused params)
    * CA1822 (Bar can be static)
    * CA2201 (Do not raise reserved exceptions)
    * CA1303 (Do not pass literals as localized params)

    * Also triggered: 
    *   common-cs:InsufficientCommentDensity
    *
    * And
    *   SonarLit S228 (do not use Console)
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

                // violates S1134
                return String.Empty; //FIXME please
            }
            set
            {
                // Violates FxCop's CA2201: Do not raise reserved exception types (major issue)
                throw new Exception("Hello world");
            }
        }
    }
}
