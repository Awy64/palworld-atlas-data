using System.Text.Json;
using System.Text.Json.Serialization;

namespace PalworldAtlas.Extractor;

internal static class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static int Main(string[] args)
    {
        try
        {
            if (args.Length == 0 || args[0] is "-h" or "--help")
            {
                PrintUsage();
                return 0;
            }

            var command = args[0].ToLowerInvariant();
            var options = ParseOptions(args[1..]);
            var pakDirectory = Required(options, "pak-dir");
            var buildId = options.GetValueOrDefault("build-id", "unknown");
            var mappings = options.GetValueOrDefault("mappings");

            return command switch
            {
                "probe" => Probe(pakDirectory, mappings, buildId, Required(options, "output")),
                "publish" => Publish(pakDirectory, mappings, buildId, Required(options, "output"),
                    options.GetValueOrDefault("game-version"), options.GetValueOrDefault("previous-manifest")),
                _ => throw new ArgumentException($"Unknown command '{command}'"),
            };
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"{exception.GetType().Name}: {exception.Message}");
            return 1;
        }
    }

    private static int Probe(string pakDirectory, string? mappings, string buildId, string output)
    {
        var report = ProbeRunner.Run(pakDirectory, mappings, buildId);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
        File.WriteAllText(output, JsonSerializer.Serialize(report, JsonOptions));
        Console.WriteLine($"Probe complete: {report.Tables.Count(table => table.Parsed)}/{report.Tables.Count} tables parsed");
        return report.ProductionGatePassed ? 0 : 2;
    }

    private static int Publish(string pakDirectory, string? mappings, string buildId, string output, string? gameVersion, string? previousManifest)
    {
        if (buildId == "unknown") throw new ArgumentException("--build-id is required for publish");
        using var workspace = new PakWorkspace(pakDirectory, mappings);
        new AtlasPublisher(workspace).Publish(output, buildId, gameVersion, previousManifest);
        Console.WriteLine($"Published validated build {buildId} to {Path.GetFullPath(output)}");
        return 0;
    }

    private static Dictionary<string, string> ParseOptions(string[] args)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < args.Length; index++)
        {
            if (!args[index].StartsWith("--", StringComparison.Ordinal))
                throw new ArgumentException($"Unexpected argument '{args[index]}'");
            var key = args[index][2..];
            if (index + 1 >= args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal))
                throw new ArgumentException($"Missing value for --{key}");
            result[key] = args[++index];
        }
        return result;
    }

    private static string Required(IReadOnlyDictionary<string, string> options, string key) =>
        options.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new ArgumentException($"--{key} is required");

    private static void PrintUsage() => Console.WriteLine("""
        Palworld Atlas data extractor

        probe   --pak-dir PATH --output FILE [--build-id ID] [--mappings FILE]
        publish --pak-dir PATH --output DIR --build-id ID [--mappings FILE]
                [--game-version VERSION] [--previous-manifest FILE]
        """);
}
