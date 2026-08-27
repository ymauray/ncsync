using Nc.Commands;
using Nc.Configuration;
using Nc.Credentials;

namespace Nc.Tests.Commands;

public sealed class ConfigCommandHandlersTests : IDisposable
{
    private readonly string _workingDirectory = Directory.CreateTempSubdirectory("nc-config-cmd-tests-").FullName;

    public void Dispose()
    {
        CredentialStoreFactory.Create().Delete(CredentialKey.ForPath(_workingDirectory));
        Directory.Delete(_workingDirectory, recursive: true);
    }

    [Fact]
    public void SetUsername_WritesUsernameToConfig()
    {
        var exitCode = ConfigCommandHandlers.SetUsername(_workingDirectory, "myname");

        Assert.Equal(0, exitCode);
        Assert.Equal("myname", new NcConfigStore(_workingDirectory).Load().Username);
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
        var output = CaptureConsoleOut(() => ConfigCommandHandlers.SetUsername(_workingDirectory, username: null));

        Assert.Equal(string.Empty, output);
    }

    [Fact]
    public void SetUsername_WithoutArgumentAndValueStored_PrintsStoredUsername()
    {
        ConfigCommandHandlers.SetUsername(_workingDirectory, "myname");

        var output = CaptureConsoleOut(() => ConfigCommandHandlers.SetUsername(_workingDirectory, username: null));

        Assert.Equal("myname", output.Trim());
    }

    [Fact]
    public void SetUsername_WithoutArgument_DoesNotOverwriteStoredValue()
    {
        ConfigCommandHandlers.SetUsername(_workingDirectory, "myname");

        ConfigCommandHandlers.SetUsername(_workingDirectory, username: null);

        Assert.Equal("myname", new NcConfigStore(_workingDirectory).Load().Username);
    }

    [Fact]
    public void SetPassword_WithoutArgument_PrintsNothingAndStoresNothing()
    {
        var output = CaptureConsoleOut(() => ConfigCommandHandlers.SetPassword(_workingDirectory, password: null));

        Assert.Equal(string.Empty, output);
        Assert.Null(CredentialStoreFactory.Create().TryLoad(CredentialKey.ForPath(_workingDirectory)));
    }

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
