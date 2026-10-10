namespace HSRGlobalMetadata.Configuration;

public sealed record CommandLineOptions(
    string? GameDirectory,
    string? OutputDirectory,
    bool GenerateDummyDll,
    bool GenerateDump,
    bool GenerateStringLiterals,
    bool Debug,
    bool ShowHelp
);

public sealed class CommandLineException(string message) : Exception(message);

public static class CommandLineParser {
    public static CommandLineOptions Parse(IReadOnlyList<string> args) {
        string? gameDirectory = null;
        string? outputDirectory = null;

        bool generateDummyDll = false;
        bool generateDump = true;
        bool generateStringLiterals = true;
        bool debug = false;
        bool showHelp = false;

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < args.Count; i++) {
            string arg = args[i];
            if (!arg.StartsWith("-", StringComparison.Ordinal)) {
                if (gameDirectory != null)
                    throw new CommandLineException($"Unexpected positional argument '{arg}'. Only one game directory is allowed.");
                gameDirectory = TrimQuotes(arg);
                continue;
            }

            string option = arg.ToLowerInvariant();

            switch (option) {
                case "-h":
                case "--help":
                    showHelp = true;
                    break;
                
                case "--dummy-dll":
                    EnsureUnique(seen, option);
                    generateDummyDll = true;
                    break;
                
                case "--no-dump":
                    EnsureUnique(seen, option);
                    generateDump = false;
                    break;

                case "--no-string-literals":
                    EnsureUnique(seen, option);
                    generateStringLiterals = false;
                    break;

                case "--debug":
                    EnsureUnique(seen, option);
                    debug = true;
                    break;

                case "--output":
                    EnsureUnique(seen, option);
                    outputDirectory = TrimQuotes(ReadValue(args, ref i, option));
                    break;

                default:
                    throw new CommandLineException($"Unknown option '{arg}'.");
            }
        }

        if (!generateDump && !generateStringLiterals && !generateDummyDll && !showHelp)
            throw new CommandLineException("No output is enabled. Remove at least one --no-* option or add --dummy-dll.");

        return new CommandLineOptions(
            gameDirectory,
            outputDirectory,
            generateDummyDll,
            generateDump,
            generateStringLiterals,
            debug,
            showHelp
        );
    }

    public static string Usage => """
        Usage:
          HSRGlobalMetadata <game-directory> [options]

        Options:
          --output <directory>        Output directory. Default: <game-directory>/dump.
          --dummy-dll                 Generate Dummy DLL assemblies.
          --no-dump                   Skip dump.cs generation.
          --no-string-literals        Skip stringliterals.json generation.
          --debug                     Print exception details when an error occurs. 
          -h, --help                  Show this help.
        """;

    private static string ReadValue(IReadOnlyList<string> args, ref int index, string option) {
        if (index + 1 >= args.Count || args[index + 1].StartsWith("-", StringComparison.Ordinal))
            throw new CommandLineException($"Option '{option}' requires a value.");
        return args[++index];
    }

    private static void EnsureUnique(HashSet<string> seen, string option) {
        if (!seen.Add(option))
            throw new CommandLineException($"Option '{option}' was specified more than once.");
    }

    private static string TrimQuotes(string value) => value.Trim().Trim('"');
}