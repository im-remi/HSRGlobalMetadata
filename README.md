# HSRGlobalMetadata

A proof-of-concept static analysis tool to extract metadata information from `Honkai: Star Rail`.

## About
HSRGlobalMetadata extracts metadata from the game's metadata files and `GameAssembly.dll` without launching the game.

This tool generates:
- `dump.cs` - a C# representation of the extracted metadata.
- `stringliterals.json` - string literals and their addresses inside the binary.
- Dummy DLL assemblies - stub assemblies generated from the extracted metadata. They can be imported into tools such as dnSpy or ILSpy.

The tool has been tested to work with the `OSPRODWin4.6.0` and `OSPRODWin4.5.0` version of the game.

## Important
This tool is a proof-of-concept. Some features may be missing, it may be unstable, or break with game updates. Older versions are not supported, and newer versions can break the tool.

## Usage
Builds of this project will not be provided.

.NET 10.0 is required.

To run it, simply do `dotnet run <path_to_game_folder>`.

For faster execution: `dotnet run -c Release <path_to_game_folder>`

### CLI Options

| Option | Description |
| ---|---|
| `--output <dir>` | Override output directory (default: `<game-directory>/dump`). |
| `--dummy-dll` | Generate Dummy DLL assemblies. |
| `--no-dump` | Skip `dump.cs` generation. |
| `--no-string-literals` | Skip `stringliterals.json` generation. |
| `--debug` | Print exception details when an error occurs. |

## Disclaimer
This tool is only for educational purposes. I do not take any responsibility for the usage of this tool.

## License
This project is licensed under the GPL-3.0 license. See [LICENSE](LICENSE) for more details.
