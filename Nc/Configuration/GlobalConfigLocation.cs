namespace Nc.Configuration;

internal static class GlobalConfigLocation
{
    public static string Directory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".config",
        "ncsync");
}
