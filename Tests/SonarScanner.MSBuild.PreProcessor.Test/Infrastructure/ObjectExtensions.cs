using System.Linq;
using System.Reflection;

namespace SonarScanner.MSBuild.PreProcessor.Test.Infrastructure;

public static class ObjectExtensions
{
    public static object Protected(this object target, string name, params object[] args) =>
        target.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(target, args);
}
