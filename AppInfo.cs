using System.Reflection;

namespace SlappyHub;

public static class AppInfo
{
    public static string Version =>
	    Assembly.GetExecutingAssembly()
		    .GetCustomAttribute<AssemblyFileVersionAttribute>()?
		    .Version
	    ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString()
	    ?? "unknown";

    public static Uri RepoUrl = new Uri("https://github.com/yasuoki/SlappyHub");
}
