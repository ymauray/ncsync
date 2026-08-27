namespace Nc.Credentials;

internal static class CredentialStorePaths
{
    public static string BaseDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData, Environment.SpecialFolderOption.Create),
        "nc",
        "credentials");
}
