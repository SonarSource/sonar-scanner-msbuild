using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Normal
{
    public class Program
    {
        static void Main(string[] args)
        {

        }

        public static string Bar
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

        public static int test(int i1, int i2, int i3)
        {
            return i1 + i2 + i3;
        }
    }
}
