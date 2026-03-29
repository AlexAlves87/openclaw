using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace OpenClaw.Shared.Tests;

public class ReadmeValidationTests
{
    [Fact]
    public void ReadmeAllowCommandsJsonExample_IsValid()
    {
        var readmePath = Path.Combine(GetRepositoryRoot(), "README.md");
        var readmeContent = File.ReadAllText(readmePath);

        var jsonPattern = @"```json\s*(\{[\s\S]*?\})\s*```";
        var matches = Regex.Matches(readmeContent, jsonPattern);
        Assert.True(matches.Count > 0, "No JSON code blocks found in README.");

        var allowCommandsJson = matches
            .Select(m => m.Groups[1].Value)
            .FirstOrDefault(json => json.Contains("\"allowCommands\"", StringComparison.Ordinal));

        Assert.NotNull(allowCommandsJson);

        using var doc = JsonDocument.Parse(allowCommandsJson!);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("gateway", out var gateway), "JSON should have a 'gateway' property.");
        Assert.True(gateway.TryGetProperty("nodes", out var nodes), "gateway should have a 'nodes' property.");
        Assert.True(nodes.TryGetProperty("allowCommands", out var allowCommands), "nodes should have an 'allowCommands' property.");
        Assert.Equal(JsonValueKind.Array, allowCommands.ValueKind);

        foreach (var command in allowCommands.EnumerateArray())
        {
            Assert.Equal(JsonValueKind.String, command.ValueKind);
        }
    }

    private static string GetRepositoryRoot()
    {
        var envRepoRoot = Environment.GetEnvironmentVariable("OPENCLAW_REPO_ROOT");
        if (!string.IsNullOrWhiteSpace(envRepoRoot) && Directory.Exists(envRepoRoot))
        {
            return envRepoRoot;
        }

        // Look for the Windows app root specifically (marked by the .slnx solution file),
        // not the monorepo root — the README we validate is apps/windows/README.md.
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (Directory.GetFiles(directory.FullName, "*.slnx").Length > 0 &&
                File.Exists(Path.Combine(directory.FullName, "README.md")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not find repository root. Set OPENCLAW_REPO_ROOT to the repo path.");
    }
}
