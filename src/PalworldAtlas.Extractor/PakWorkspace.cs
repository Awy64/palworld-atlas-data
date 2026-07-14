using System.Diagnostics;
using CUE4Parse.Compression;
using CUE4Parse.FileProvider;
using CUE4Parse.MappingsProvider;
using CUE4Parse.MappingsProvider.Usmap;
using CUE4Parse.UE4.Assets.Exports.Engine;
using CUE4Parse.UE4.Versions;
using Serilog;

namespace PalworldAtlas.Extractor;

internal sealed class PakWorkspace : IDisposable
{
    private readonly DefaultFileProvider _provider;
    private readonly string _pakDirectory;
    private readonly Dictionary<string, UDataTable?> _tables = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _errors = new(StringComparer.OrdinalIgnoreCase);

    public PakWorkspace(string pakDirectory, string? mappingsPath)
    {
        _pakDirectory = pakDirectory;
        Log.Logger = new LoggerConfiguration().MinimumLevel.Warning().WriteTo.Console().CreateLogger();
        OodleHelper.Initialize();
        _provider = new DefaultFileProvider(pakDirectory, SearchOption.AllDirectories,
            new VersionContainer(EGame.GAME_UE5_1), StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(mappingsPath))
            _provider.MappingsContainer = new FileUsmapTypeMappingsProvider(mappingsPath);
        _provider.Initialize();
        _provider.Mount();
        _provider.LoadVirtualPaths();
    }

    public long PakBytes => Directory.EnumerateFiles(_pakDirectory, "*.pak", SearchOption.AllDirectories)
        .Sum(path => new FileInfo(path).Length);

    public bool PackageExists(string packagePath)
    {
        var needle = $"{packagePath}.uasset";
        return _provider.Files.Any(file => file.Key.Equals(needle, StringComparison.OrdinalIgnoreCase));
    }

    public UDataTable? LoadFirst(TableSpec spec)
    {
        foreach (var path in spec.PackagePaths)
        {
            var table = Load(path);
            if (table is not null) return table;
        }
        return null;
    }

    public IReadOnlyList<UDataTable> LoadAll(TableSpec spec) => spec.PackagePaths
        .Select(Load)
        .Where(table => table is not null)
        .Cast<UDataTable>()
        .ToArray();

    public UDataTable? Load(string packagePath)
    {
        if (_tables.TryGetValue(packagePath, out var cached)) return cached;
        if (!PackageExists(packagePath))
        {
            _tables[packagePath] = null;
            return null;
        }
        try
        {
            var table = _provider.LoadPackageObject<UDataTable>(packagePath);
            _tables[packagePath] = table;
            return table;
        }
        catch (Exception exception)
        {
            _errors[packagePath] = $"{exception.GetType().Name}: {exception.Message}";
            _tables[packagePath] = null;
            return null;
        }
    }

    public SourceTableStatus Probe(TableSpec spec)
    {
        var presentPath = spec.PackagePaths.FirstOrDefault(PackageExists);
        if (presentPath is null)
            return new SourceTableStatus(spec.Name, null, false, false, 0, [], "Package not present in the dedicated server PAK");

        var table = Load(presentPath);
        if (table is null)
            return new SourceTableStatus(spec.Name, presentPath, true, false, 0, [], _errors.GetValueOrDefault(presentPath, "Unable to deserialize table"));

        var sampleFields = table.RowMap.Values.Take(3)
            .SelectMany(row => new RowReader(row).FieldNames)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .Take(80)
            .ToArray();
        return new SourceTableStatus(spec.Name, presentPath, true, true, table.RowMap.Count, sampleFields, null);
    }

    public void Dispose()
    {
        _provider.Dispose();
        Log.CloseAndFlush();
    }
}

internal static class ProbeRunner
{
    public static ProbeReport Run(string pakDirectory, string? mappingsPath, string buildId)
    {
        var started = DateTimeOffset.UtcNow;
        var process = Process.GetCurrentProcess();
        using var workspace = new PakWorkspace(pakDirectory, mappingsPath);
        var tables = AssetCatalog.All.Select(workspace.Probe).ToArray();
        var required = AssetCatalog.All.Where(spec => spec.Required).Select(spec => spec.Name).ToHashSet();
        var passed = tables.Where(table => required.Contains(table.Name)).All(table => table.Parsed && table.RowCount > 0);
        var notes = new List<string>();
        if (string.IsNullOrWhiteSpace(mappingsPath))
            notes.Add("No mappings file was supplied. Palworld unversioned properties may prevent row deserialization.");
        if (!tables.Single(table => table.Name == AssetCatalog.MapMetadata.Name).Present)
            notes.Add("Client-only map metadata is absent; the checked Palpagos transform will be used and unsupported regions remain unpublished.");

        return new ProbeReport(
            buildId,
            started,
            DateTimeOffset.UtcNow,
            workspace.PakBytes,
            process.PeakWorkingSet64,
            !string.IsNullOrWhiteSpace(mappingsPath),
            passed,
            tables,
            notes);
    }
}
