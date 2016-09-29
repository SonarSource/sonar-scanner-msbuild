using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Normal;

namespace Test
{
    [TestClass]
    public class UnitTest1
    {
        [TestMethod]
        public void TestMethod1()
        {
            // violates S2228 
            Console.WriteLine(new Program().Bar);

            // violates S1135
            //TODO: lorem ipsum 
        }

        public string Bar
        {
            get
            {
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
