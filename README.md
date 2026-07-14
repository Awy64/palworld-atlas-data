# Palworld Atlas Data

Automated, versioned factual data for Palworld Atlas. A GitHub-hosted Linux runner downloads the anonymous Palworld Dedicated Server package, inventories its Unreal Engine tables, normalizes supported records, validates the resulting contract, and publishes JSON through GitHub Pages.

Published pointer: [`v1/latest.json`](https://awy64.github.io/palworld-atlas-data/v1/latest.json)

## Safety and provenance

- Steam app `2394010` is downloaded only when its public build ID changes.
- Raw PAK files, extracted tables, textures, mappings, and other game assets are never committed or uploaded.
- A failed extraction cannot replace the last successful published build.
- The repository publishes normalized facts only: Pal and item fields, breeding rules, and spawn coordinates.

## Workflows

- **Probe Palworld server package** is a manual feasibility gate. It records table presence, row counts, property names, elapsed time, and disk use without retaining extracted content.
- **Refresh Atlas data** runs every six hours and on demand. It exits before downloading when the Steam build ID is unchanged.

The current Linux server build exposes the required table properties without an external Unreal mapping file. `PALWORLD_MAPPINGS_URL` remains an optional repository variable (and manual workflow override) if a future build requires a compatible `Mappings.usmap` file.

## Local commands

```sh
dotnet test
dotnet run --project src/PalworldAtlas.Extractor -- probe --pak-dir /path/to/Pal/Content/Paks --output probe-report.json --mappings /path/to/Mappings.usmap
dotnet run --project src/PalworldAtlas.Extractor -- publish --pak-dir /path/to/Pal/Content/Paks --output ./published --build-id 12345678 --mappings /path/to/Mappings.usmap
```

This is an unofficial fan project and is not affiliated with Pocketpair.
