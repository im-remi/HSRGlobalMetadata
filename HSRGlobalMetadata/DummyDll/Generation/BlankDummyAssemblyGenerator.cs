using HSRGlobalMetadata.Structs.Definitions;
using Mono.Cecil;

namespace HSRGlobalMetadata.DummyDll.Generation;

internal sealed class BlankDummyAssemblyGenerator : IDisposable {
    private static readonly string[] RequiredTemplateTypes = [
        "AddressAttribute",
        "FieldOffsetAttribute",
        "MetadataOffsetAttribute",
        "TokenAttribute"
    ];

    private readonly DummyAssemblyResolver _resolver = new();
    private readonly List<AssemblyDefinition> _assemblies = [];

    public IReadOnlyList<AssemblyDefinition> Assemblies => _assemblies;
    public DummyAssemblyResolver Resolver => _resolver;

    public BlankDummyAssemblyGenerator(IEnumerable<Il2CppImageDefinition> images)
        : this(images?.Select(image => image.Name) ?? throw new ArgumentNullException(nameof(images))) {
    }

    internal BlankDummyAssemblyGenerator(IEnumerable<string> imageNames) {
        ArgumentNullException.ThrowIfNull(imageNames);

        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AssemblyDefinition template = ReadTemplate();
        AddAssembly(template, names);

        foreach (string imageName in imageNames) {
            string moduleName = NormalizeModuleName(imageName);
            string assemblyName = Path.GetFileNameWithoutExtension(moduleName);
            var nameDefinition = new AssemblyNameDefinition(assemblyName, new Version(0, 0, 0, 0));
            var moduleParameters = new ModuleParameters {
                Kind = ModuleKind.Dll,
                AssemblyResolver = _resolver
            };
            AssemblyDefinition assembly = AssemblyDefinition.CreateAssembly(nameDefinition, moduleName, moduleParameters);
            AddAssembly(assembly, names);
        }
    }

    private AssemblyDefinition ReadTemplate() {
        byte[] bytes = EmbeddedDummyDllTemplate.Read();
        var parameters = new ReaderParameters {
            AssemblyResolver = _resolver,
            InMemory = true,
            ReadWrite = false
        };
        var assembly = AssemblyDefinition.ReadAssembly(new MemoryStream(bytes, writable: false), parameters);

        var available = assembly.MainModule.Types.Select(type => type.Name).ToHashSet(StringComparer.Ordinal);
        string[] missing = RequiredTemplateTypes.Where(type => !available.Contains(type)).ToArray();
        if (missing.Length > 0) {
            assembly.Dispose();
            throw new InvalidDataException($"Il2CppDummyDll template is missing required types: {string.Join(", ", missing)}.");
        }

        return assembly;
    }

    private void AddAssembly(AssemblyDefinition assembly, HashSet<string> names) {
        string moduleName = assembly.MainModule.Name;
        if (!names.Add(moduleName)) {
            assembly.Dispose();
            throw new InvalidDataException($"Duplicate DummyDll output module name '{moduleName}'.");
        }
        _resolver.Register(assembly);
        _assemblies.Add(assembly);
    }

    internal static string NormalizeModuleName(string? imageName) {
        string? fileName = Path.GetFileName(imageName?.Trim());
        if (string.IsNullOrWhiteSpace(fileName))
            throw new InvalidDataException("Metadata image has an empty assembly name.");

        char[] invalid = Path.GetInvalidFileNameChars();
        string sanitized = new(fileName.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray());
        if (!sanitized.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            sanitized += ".dll";
        return sanitized;
    }

    public void Dispose() {
        foreach (AssemblyDefinition assembly in _assemblies)
            assembly.Dispose();
        _assemblies.Clear();
        _resolver.Dispose();
    }
}
