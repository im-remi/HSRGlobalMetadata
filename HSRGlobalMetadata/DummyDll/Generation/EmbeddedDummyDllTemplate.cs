using System.Reflection;

namespace HSRGlobalMetadata.DummyDll.Generation;

internal static class EmbeddedDummyDllTemplate {
    private const string ResourceName = "HSRGlobalMetadata.Resources.Il2CppDummyDll.dll";

    public static byte[] Read() {
        Assembly assembly = typeof(EmbeddedDummyDllTemplate).Assembly;
        using Stream? stream = assembly.GetManifestResourceStream(ResourceName);
        if (stream == null) {
            string available = string.Join(", ", assembly.GetManifestResourceNames().OrderBy(name => name));
            throw new InvalidOperationException(
                $"Embedded DummyDll template '{ResourceName}' was not found. Available resources: {available}"
            );
        }

        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }
}
