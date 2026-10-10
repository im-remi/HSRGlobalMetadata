using Mono.Cecil;
using System.Text.Json;
using HSRGlobalMetadata.DummyDll.Adapters;
using HSRGlobalMetadata.DummyDll.Generation;

namespace HSRGlobalMetadata.DummyDll.Output;

public static class DummyAssemblyExporter {
    public const string DirectoryName = "DummyDll";

    public static void ExportAssemblies(string outputRoot) {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputRoot);

        string normalizedRoot = Path.GetFullPath(outputRoot);
        Directory.CreateDirectory(normalizedRoot);

        string targetDirectory = Path.Combine(normalizedRoot, DirectoryName);
        string stagingDirectory = Path.Combine(normalizedRoot, $".{DirectoryName}.{Guid.NewGuid():N}.staging");
        string backupDirectory = Path.Combine(normalizedRoot, $".{DirectoryName}.{Guid.NewGuid():N}.backup");

        Directory.CreateDirectory(stagingDirectory);
        try {
            var source = new HsrDummyMetadataSource();
            using var generator = new MetadataDummyAssemblyGenerator(source);
            var outputFiles = new List<string>(generator.Assemblies.Count);

            foreach (AssemblyDefinition assembly in generator.Assemblies) {
                string fileName = BlankDummyAssemblyGenerator.NormalizeModuleName(assembly.MainModule.Name);
                string path = Path.Combine(stagingDirectory, fileName);
                assembly.Write(path);
                ValidateAssembly(path, generator.Resolver);
                outputFiles.Add(fileName);
            }

            const string reportFileName = "generation-report.json";
            string stagingReportPath = Path.Combine(stagingDirectory, reportFileName);
            File.WriteAllText(stagingReportPath, JsonSerializer.Serialize(generator.Report, new JsonSerializerOptions {
                WriteIndented = true
            }));

            ReplaceDirectory(stagingDirectory, targetDirectory, backupDirectory);
        }
        finally {
            DeleteDirectoryIfPresent(stagingDirectory);
            DeleteDirectoryIfPresent(backupDirectory);
        }
    }

    private static void ValidateAssembly(string path, IAssemblyResolver resolver) {
        var parameters = new ReaderParameters {
            AssemblyResolver = resolver,
            InMemory = true,
            ReadWrite = false
        };
        using AssemblyDefinition assembly = AssemblyDefinition.ReadAssembly(path, parameters);
        if (!string.Equals(assembly.MainModule.Name, Path.GetFileName(path), StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"Generated module name '{assembly.MainModule.Name}' does not match file '{path}'.");
    }

    private static void ReplaceDirectory(string stagingDirectory, string targetDirectory, string backupDirectory) {
        bool movedExisting = false;
        if (Directory.Exists(targetDirectory)) {
            Directory.Move(targetDirectory, backupDirectory);
            movedExisting = true;
        }

        try {
            Directory.Move(stagingDirectory, targetDirectory);
            if (movedExisting)
                DeleteDirectoryIfPresent(backupDirectory);
        } catch {
            if (!Directory.Exists(targetDirectory) && movedExisting && Directory.Exists(backupDirectory))
                Directory.Move(backupDirectory, targetDirectory);
            throw;
        }
    }

    private static void DeleteDirectoryIfPresent(string path) {
        if (Directory.Exists(path))
            Directory.Delete(path, recursive: true);
    }
}
