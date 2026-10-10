using HSRGlobalMetadata.Configuration;
using HSRGlobalMetadata.DummyDll.Generation;
using HSRGlobalMetadata.DummyDll.Output;
using HSRGlobalMetadata.Output;
using HSRGlobalMetadata.Structs;
using HSRGlobalMetadata.Utils;

namespace HSRGlobalMetadata;

public static class Program {
    public static int Main(string[] args) {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        CommandLineOptions options;
        try {
            options = CommandLineParser.Parse(args);
        } catch (CommandLineException ex) {
            Console.Error.WriteLine($"Argument error: {ex.Message}");
            Console.Error.WriteLine(CommandLineParser.Usage);
            return 2;
        }

        if (options.ShowHelp) {
            Console.WriteLine(CommandLineParser.Usage);
            return 0;
        }

        string folderPath = options.GameDirectory ?? Prompt();
        folderPath = Path.GetFullPath(folderPath.Trim().Trim('"'));
        if (!Directory.Exists(folderPath))
            return Fail($"Game directory does not exist: {folderPath}");

        string gameAssemblyPath = Path.Combine(folderPath, "GameAssembly.dll");
        string metadataPath = Path.Combine(folderPath, "StarRail_Data", "il2cpp_data", "Metadata", "global-metadata.dat");
        string startupMetadataPath = Path.Combine(folderPath, "StarRail_Data", "il2cpp_data", "Metadata", "startup-metadata.dat");

        if (!File.Exists(gameAssemblyPath))
            return Fail($"GameAssembly.dll not found: {gameAssemblyPath}");
        if (!File.Exists(metadataPath))
            return Fail($"global-metadata.dat not found: {metadataPath}");
        if (!File.Exists(startupMetadataPath))
            return Fail($"startup-metadata.dat not found: {startupMetadataPath}");

        string outputRoot = options.OutputDirectory == null
            ? Path.Combine(folderPath, "dump")
            : Path.GetFullPath(options.OutputDirectory);

        try {
            PEHelper.ReadPEHeader(gameAssemblyPath);

            Console.WriteLine("Initializing metadata...");
            MetadataContext.Initialize(metadataPath, startupMetadataPath, gameAssemblyPath);
            MetadataHeader.Initialize(gameAssemblyPath);
            MetadataRegistration.Initialize(gameAssemblyPath);
            CodeRegistration.Initialize(gameAssemblyPath);
            MetadataTables.Initialize(gameAssemblyPath);
            Console.WriteLine("Initializing cache...");
            MetadataCache.Initialize();
            Console.WriteLine("Initialization complete.");

            if (options.GenerateDump) {
                Console.WriteLine("Writing dump.cs...");
                DumpWriter.WriteToDirectory(outputRoot);
            }

            if (options.GenerateStringLiterals) {
                Console.WriteLine("Writing stringliterals.json...");
                StringLiteralWriter.WriteToDirectory(outputRoot);
            }

            if (options.GenerateDummyDll) {
                Console.WriteLine("Writing Dummy DLL assemblies, this will take a while...");
                DummyAssemblyExporter.ExportAssemblies(outputRoot);
            }

            Console.WriteLine("Finished.");
            return 0;
        }
        catch (Exception ex) {
            Console.Error.WriteLine($"Failed: {ex.Message}");
            if (options.Debug)
                Console.Error.WriteLine(ex);
            return 1;
        }
    }

    private static int Fail(string message, int exitCode = 1) {
        Console.Error.WriteLine(message);
        return exitCode;
    }

    private static string Prompt() {
        Console.Write("Enter game folder: ");
        return Console.ReadLine() ?? "";
    }
}
