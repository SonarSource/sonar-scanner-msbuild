using System;
using System.Collections.Generic;
using System.Linq;

using Foundation;
using UIKit;

namespace XamarinApplication.iOS
{
    public class Application
    {
        // This is the main entry point of the application.
        static void Main(string[] args)
        {
            // if you want to use a different Application Delegate class from "AppDelegate"
            // you can specify it here.
            UIApplication.Main(args, null, "AppDelegate");
        }

        /// <summary>
        ///  This empty method is used to see that the issues reported are correctly imported by the scanner.
        /// </summary>
        public static void EmptyMethod()
        {
        }
    }
}
