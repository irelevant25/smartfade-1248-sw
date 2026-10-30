namespace SmartFade.Communication.Tests;

/// <summary>Locates committed captures relative to the repository root (the folder holding SmartFade.sln).</summary>
public static class TestPaths
{
    public static string RepositoryRoot { get; } = FindRepositoryRoot();

    public static string Capture(string fileName) => Path.Combine(RepositoryRoot, "docs", "protocol", "captures", fileName);

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "SmartFade.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("SmartFade.sln not found above " + AppContext.BaseDirectory);
    }
}
