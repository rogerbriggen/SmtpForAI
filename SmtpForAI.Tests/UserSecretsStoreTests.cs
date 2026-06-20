using SmtpForAI.Configuration;

namespace SmtpForAI.Tests;

[TestClass]
public sealed class UserSecretsStoreTests
{
    private string _dir = "";
    private string Path_ => System.IO.Path.Combine(_dir, "secrets.json");

    [TestInitialize]
    public void Setup()
    {
        _dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "smtpforai-tests-" + Guid.NewGuid().ToString("N"));
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }

    [TestMethod]
    public void ReadPassword_returns_null_when_file_missing()
    {
        Assert.IsNull(UserSecretsStore.ReadPasswordFrom(Path_));
    }

    [TestMethod]
    public void Write_then_read_round_trips()
    {
        UserSecretsStore.WritePasswordTo(Path_, "hunter2");
        Assert.AreEqual("hunter2", UserSecretsStore.ReadPasswordFrom(Path_));
    }

    [TestMethod]
    public void Write_preserves_unrelated_keys()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(Path_, """{ "Other:Key": "keep-me" }""");

        UserSecretsStore.WritePasswordTo(Path_, "pw");

        var json = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(Path_))!.AsObject();
        Assert.AreEqual("keep-me", json["Other:Key"]!.GetValue<string>());
        Assert.AreEqual("pw", json[UserSecretsStore.PasswordKey]!.GetValue<string>());
    }

    [TestMethod]
    public void Read_tolerates_corrupt_file()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(Path_, "{ not valid json");
        Assert.IsNull(UserSecretsStore.ReadPasswordFrom(Path_));
    }
}
