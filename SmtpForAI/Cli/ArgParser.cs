namespace SmtpForAI.Cli;

/// <summary>
/// Minimal, dependency-free command-line parser. Supports
/// <c>--flag value</c>, <c>--flag=value</c>, bare boolean <c>--flag</c>,
/// repeatable flags (e.g. multiple <c>--to</c>), and positional arguments.
/// </summary>
internal sealed class ArgParser
{
    private readonly Dictionary<string, List<string>> _options;
    private readonly List<string> _positionals;

    private ArgParser(Dictionary<string, List<string>> options, List<string> positionals)
    {
        _options = options;
        _positionals = positionals;
    }

    public IReadOnlyList<string> Positionals => _positionals;

    /// <param name="booleanFlags">
    /// Flags that never consume the following token (e.g. <c>--html</c>, <c>--json</c>).
    /// </param>
    public static ArgParser Parse(IEnumerable<string> args, ISet<string>? booleanFlags = null)
    {
        booleanFlags ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var options = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var positionals = new List<string>();
        var tokens = args.ToList();

        for (int i = 0; i < tokens.Count; i++)
        {
            var token = tokens[i];
            if (!token.StartsWith("--", StringComparison.Ordinal))
            {
                positionals.Add(token);
                continue;
            }

            var name = token[2..];
            string? inlineValue = null;
            var eq = name.IndexOf('=');
            if (eq >= 0)
            {
                inlineValue = name[(eq + 1)..];
                name = name[..eq];
            }

            if (!options.TryGetValue(name, out var values))
            {
                values = new List<string>();
                options[name] = values;
            }

            if (inlineValue is not null)
            {
                values.Add(inlineValue);
            }
            else if (booleanFlags.Contains(name))
            {
                values.Add("true");
            }
            else if (i + 1 < tokens.Count && !tokens[i + 1].StartsWith("--", StringComparison.Ordinal))
            {
                values.Add(tokens[++i]);
            }
            else
            {
                // Present without a value (and not a declared boolean): treat as a flag.
                values.Add("true");
            }
        }

        return new ArgParser(options, positionals);
    }

    public bool Has(string name) => _options.ContainsKey(name);

    /// <summary>Last value supplied for <paramref name="name"/>, or null.</summary>
    public string? Get(string name) =>
        _options.TryGetValue(name, out var values) && values.Count > 0 ? values[^1] : null;

    /// <summary>All values supplied for a repeatable flag, in order.</summary>
    public IReadOnlyList<string> GetAll(string name) =>
        _options.TryGetValue(name, out var values) ? values : Array.Empty<string>();
}
