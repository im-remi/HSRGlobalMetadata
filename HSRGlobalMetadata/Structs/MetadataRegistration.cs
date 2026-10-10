using HSRGlobalMetadata.Utils;

namespace HSRGlobalMetadata.Structs;

public class MetadataRegistration : MetadataBase {
    private static MetadataRegistration? _instance;
    public static MetadataRegistration Instance => _instance ?? throw new Exception("Not initialized");

    public MetadataRegistration(byte[] bytes) : base(bytes) {
        Populate();
    }
    
    public static void Initialize(string gameAssemblyPath) {
        long metadataRegistrationPtr = RegisterPointersFunction.Initialize(gameAssemblyPath).GetMetadataRegistration();
        byte[] bytes = new ArraySegment<byte>(MetadataContext.Instance.GameAssembly, (int)metadataRegistrationPtr, 0x100).ToArray();
        _instance = new MetadataRegistration(bytes);
    }

    private long ToFileOffset(long va) => va - checked((long)PEHelper.ImageBase);
    private long ReadPtr(int offset) => ToFileOffset(BitConverter.ToInt64(_bytes, offset));

    [MetadataTag(0x48, MetadataOperation.SUB, 1455078204)]
    public int TypeInfoCount { get; set; }
    
    public long TypesRva { get; private set; }
    public long GenericInstsOffset { get; private set; }
    public long ArrayOffset { get; set; }
    
    protected override void PostProcess() {
        GenericInstsOffset = ReadPtr(0x38);
        TypesRva = ReadPtr(0x80);
        ArrayOffset = ReadPtr(0x70);
    }
}
