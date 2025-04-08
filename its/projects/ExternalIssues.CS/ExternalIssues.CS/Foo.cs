namespace CSProj1
{
    /* SonarLint violations:
    *   S1134 (no 'F i x m e' comments)  : on in QP, off in local ruleset
    *   S1135 (no 'T O D O' comments)    : off in both QP and ruleset
    *   S112 (do not throw Exception)    : off in QP, on in local ruleset
    *   S125 (remove commented out code) : on in both, error in local ruleset
    *
    *  External issues: (only reported when SQ >= v7.4)
    *   Wintellect004 : use "String"
    *
    *   Project level issue - won't be reported in SQ: Wintellect008 : add a filled out AssemblyDescriptionAttribute
    */

    public class Foo
    {
        public string Bar // Violates Wintellect004 - use "String"
        {
            get
            {
                // violates S1135
                //TODO: lorem ipsum

                // violates S125
                // var i = 1;

                // violates S1134
                return String.Empty; //FIXME please
            }
            set
            {
                // Violates S112: Do not raise reserved exception types (major issue)
                throw new Exception("Hello world");
            }
        }
    }
}
