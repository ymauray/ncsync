using Nc.Commands;
using Nc.Configuration;
using Nc.Credentials;

namespace Nc.Tests.Commands;

public sealed class ConfigCommandHandlersTests : IDisposable
{
    private readonly string _workingDirectory = Directory.CreateTempSubdirectory("nc-config-cmd-tests-").FullName;

    // Repertoire global isole, distinct du vrai ~/.config/ncsync : les tests ne doivent jamais
    // lire ni ecrire dans la vraie configuration globale de la machine qui les execute.
    private readonly string _globalConfigDirectory = Directory.CreateTempSubdirectory("nc-config-global-tests-").FullName;

    public void Dispose()
    {
        CredentialStoreFactory.Create().Delete(CredentialKey.ForPath(_workingDirectory));
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
    public void SetPassword_StoresPasswordInCredentialStore()
    {
        var exitCode = ConfigCommandHandlers.SetPassword(_workingDirectory, "s3cret");

        Assert.Equal(0, exitCode);
        var stored = CredentialStoreFactory.Create().TryLoad(CredentialKey.ForPath(_workingDirectory));
        Assert.Equal("s3cret", stored);
    }

    [Fact]
    public void SetPassword_DoesNotWriteToNcConfig()
    {
        ConfigCommandHandlers.SetPassword(_workingDirectory, "s3cret");

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
        var output = CaptureConsoleOut(() => ConfigCommandHandlers.SetPassword(_workingDirectory, password: null));

        Assert.Equal(string.Empty, output);
        Assert.Null(CredentialStoreFactory.Create().TryLoad(CredentialKey.ForPath(_workingDirectory)));
    }

    private int SetUsername(string? username) =>
        ConfigCommandHandlers.SetUsername(_workingDirectory, username, _globalConfigDirectory);

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
