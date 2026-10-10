using HSRGlobalMetadata.Utils;

namespace HSRGlobalMetadata.Structs;

public class CodeRegistration : MetadataBase {
    private static CodeRegistration? _instance;
    public static CodeRegistration Instance => _instance ?? throw new Exception("Not initialized");

    public CodeRegistration(byte[] bytes) : base(bytes) {
        Populate();
    }
    
    public static void Initialize(string gameAssemblyPath) {
        if (_instance != null) return;
        long codeRegistrationPtr = RegisterPointersFunction.Initialize(gameAssemblyPath).GetCodeRegistration();
        byte[] bytes = new ArraySegment<byte>(MetadataContext.Instance.GameAssembly, (int)codeRegistrationPtr, 0x100).ToArray();
        _instance = new CodeRegistration(bytes);
    }

    private long ToFileOffset(long va) => va - checked((long)PEHelper.ImageBase);
    private long ReadPtr(int offset) => ToFileOffset(BitConverter.ToInt64(_bytes, offset));

    public long MethodPointer { get; private set; }

    protected override void PostProcess() {
        MethodPointer = ReadPtr(0x40);
    }
}
