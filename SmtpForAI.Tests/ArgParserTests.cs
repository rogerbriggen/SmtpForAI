using SmtpForAI.Cli;

namespace SmtpForAI.Tests;

[TestClass]
public sealed class ArgParserTests
{
    [TestMethod]
    public void Parses_flag_with_value()
    {
        var p = ArgParser.Parse(["--subject", "Hello"]);
        Assert.AreEqual("Hello", p.Get("subject"));
        Assert.IsTrue(p.Has("subject"));
    }

    [TestMethod]
    public void Parses_inline_equals_value()
    {
        var p = ArgParser.Parse(["--host=smtp.example.com"]);
        Assert.AreEqual("smtp.example.com", p.Get("host"));
    }

    [TestMethod]
    public void Collects_repeated_flags_in_order()
    {
        var p = ArgParser.Parse(["--to", "a@x.com", "--to", "b@x.com"]);
        CollectionAssert.AreEqual(new[] { "a@x.com", "b@x.com" }, p.GetAll("to").ToArray());
        Assert.AreEqual("b@x.com", p.Get("to"), "Get returns the last value");
    }

    [TestMethod]
    public void Boolean_flag_does_not_consume_next_token()
    {
        var p = ArgParser.Parse(["--json", "--subject", "Hi"], new HashSet<string> { "json" });
        Assert.IsTrue(p.Has("json"));
        Assert.AreEqual("Hi", p.Get("subject"));
    }

    [TestMethod]
    public void Trailing_flag_without_value_is_treated_as_present()
    {
        var p = ArgParser.Parse(["--dry-run"]);
        Assert.IsTrue(p.Has("dry-run"));
    }

    [TestMethod]
    public void Collects_positionals()
    {
        var p = ArgParser.Parse(["set", "--host", "x"]);
        CollectionAssert.AreEqual(new[] { "set" }, p.Positionals.ToArray());
    }

    [TestMethod]
    public void Unknown_flag_returns_null()
    {
        var p = ArgParser.Parse(["--to", "a@x.com"]);
        Assert.IsNull(p.Get("subject"));
        Assert.IsFalse(p.Has("subject"));
    }
}
