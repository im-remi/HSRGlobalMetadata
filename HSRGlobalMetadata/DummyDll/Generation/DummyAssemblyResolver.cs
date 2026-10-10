using Mono.Cecil;

namespace HSRGlobalMetadata.DummyDll.Generation;

internal sealed class DummyAssemblyResolver : DefaultAssemblyResolver {
    public void Register(AssemblyDefinition assembly) {
        ArgumentNullException.ThrowIfNull(assembly);
        RegisterAssembly(assembly);
    }
}
