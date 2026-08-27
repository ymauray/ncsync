using Nc.Commands;
using Nc.Configuration;
using Nc.Credentials;

namespace Nc.Tests.Commands;

public sealed class ConfigCommandHandlersTests : IDisposable
{
    private readonly string _workingDirectory = Directory.CreateTempSubdirectory("nc-config-cmd-tests-").FullName;

    // Repertoire/cle globaux isoles, distincts des vrais ~/.config/ncsync et de la cle
    // globale reelle : les tests ne doivent jamais lire ni ecrire la vraie configuration
    // globale de la machine qui les execute.
    private readonly string _globalConfigDirectory = Directory.CreateTempSubdirectory("nc-config-global-tests-").FullName;
    private readonly string _globalCredentialKey = $"nc-tests-global-{Guid.NewGuid():N}";

    public void Dispose()
    {
        CredentialStoreFactory.Create().Delete(CredentialKey.ForPath(_workingDirectory));
        CredentialStoreFactory.Create().Delete(_globalCredentialKey);
        Directory.Delete(_workingDirectory, recursive: true);
        Directory.Delete(_globalConfigDirectory, recursive: true);
    }

    [Fact]
    public void SetUsername_WritesUsernameToGlobalConfig()
    {
        var exitCode = SetUsername("myname");

        Assert.Equal(0, exitCode);
        Assert.Equal("myname", new IdentityConfigStore(_workingDirectory, _globalConfigDirectory).Load().Username);
    }

    [Fact]
    public void SetUsername_DoesNotWriteToLocalNcConfig()
    {
        SetUsername("myname");

        Assert.Null(new NcConfigStore(_workingDirectory).Load().Username);
    }

    [Fact]
    public void SetPassword_StoresPasswordUnderGlobalKey()
    {
        var exitCode = SetPassword("s3cret");

        Assert.Equal(0, exitCode);
        var stored = CredentialStoreFactory.Create().TryLoad(_globalCredentialKey);
        Assert.Equal("s3cret", stored);
    }

    [Fact]
    public void SetPassword_DoesNotWriteToNcConfig()
    {
        SetPassword("s3cret");

        Assert.Null(new NcConfigStore(_workingDirectory).Load().Username);
    }

    [Fact]
    public void SetUsername_WithoutArgumentAndNothingStored_PrintsNothing()
    {
        var output = CaptureConsoleOut(() => SetUsername(null));

        Assert.Equal(string.Empty, output);
    }

    [Fact]
    public void SetUsername_WithoutArgumentAndValueStored_PrintsStoredUsername()
    {
        SetUsername("myname");

        var output = CaptureConsoleOut(() => SetUsername(null));

        Assert.Equal("myname", output.Trim());
    }

    [Fact]
    public void SetUsername_WithoutArgument_DoesNotOverwriteStoredValue()
    {
        SetUsername("myname");

        SetUsername(null);

        Assert.Equal("myname", new IdentityConfigStore(_workingDirectory, _globalConfigDirectory).Load().Username);
    }

    [Fact]
    public void SetPassword_WithoutArgument_PrintsNothingAndStoresNothing()
    {
        var output = CaptureConsoleOut(() => SetPassword(null));

        Assert.Equal(string.Empty, output);
        Assert.Null(CredentialStoreFactory.Create().TryLoad(_globalCredentialKey));
    }

    private int SetUsername(string? username) =>
        ConfigCommandHandlers.SetUsername(_workingDirectory, username, _globalConfigDirectory);

    private int SetPassword(string? password) =>
        ConfigCommandHandlers.SetPassword(_workingDirectory, password, _globalCredentialKey);

    private static string CaptureConsoleOut(Action action)
    {
        var originalOut = Console.Out;
        var writer = new StringWriter();
        Console.SetOut(writer);
        try
        {
            action();
        }
        finally
        {
            Console.SetOut(originalOut);
        }
        return writer.ToString();
    }
}
