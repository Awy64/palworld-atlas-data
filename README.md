# Palworld Atlas Data

Automated, versioned factual data for Palworld Atlas. A GitHub-hosted Linux runner downloads the anonymous Palworld Dedicated Server package, inventories its Unreal Engine tables, normalizes supported records, validates the resulting contract, and publishes JSON through GitHub Pages.

## Safety and provenance

- Steam app `2394010` is downloaded only when its public build ID changes.
- Raw PAK files, extracted tables, textures, mappings, and other game assets are never committed or uploaded.
- A failed extraction cannot replace the last successful published build.
- The repository publishes normalized facts only: Pal and item fields, breeding rules, and spawn coordinates.

## Workflows

- **Probe Palworld server package** is a manual feasibility gate. It records table presence, row counts, property names, elapsed time, and disk use without retaining extracted content.
- **Refresh Atlas data** runs every six hours and on demand. It exits before downloading when the Steam build ID is unchanged.

Palworld uses unversioned Unreal properties, so a mapping file matching the current game build may be required. Configure the repository variable `PALWORLD_MAPPINGS_URL` to a trusted, version-compatible mapping source. The probe reports a clear failure without publishing when mappings are absent or stale.

## Local commands

```sh
dotnet test
dotnet run --project src/PalworldAtlas.Extractor -- probe --pak-dir /path/to/Pal/Content/Paks --output probe-report.json --mappings /path/to/Mappings.usmap
dotnet run --project src/PalworldAtlas.Extractor -- publish --pak-dir /path/to/Pal/Content/Paks --output ./published --build-id 12345678 --mappings /path/to/Mappings.usmap
```

This is an unofficial fan project and is not affiliated with Pocketpair.
