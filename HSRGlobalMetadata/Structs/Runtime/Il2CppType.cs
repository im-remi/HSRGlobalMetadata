using HSRGlobalMetadata.Configuration;
using HSRGlobalMetadata.Structs.Definitions;
using HSRGlobalMetadata.Utils;
using System.Collections.Concurrent;
using System.Linq.Expressions;

namespace HSRGlobalMetadata.Structs.Runtime;

public class Il2CppType {
    static readonly ConcurrentDictionary<int, string> GenericClassNameCache = new();

    static readonly Dictionary<byte, string> PrimitiveTypes = new() {
        [0x01] = "void",
        [0x02] = "bool",
        [0x03] = "char",
        [0x04] = "sbyte",
        [0x05] = "byte",
        [0x06] = "short",
        [0x07] = "ushort",
        [0x08] = "int",
        [0x09] = "uint",
        [0x0A] = "long",
        [0x0B] = "ulong",
        [0x0C] = "float",
        [0x0D] = "double",
        [0x0E] = "string",
        [0x16] = "TypedReference",
        [0x18] = "IntPtr",
        [0x19] = "UIntPtr",
        [0x1C] = "object",
    };
    
    public ulong Data;
    public ushort Attrs;
    public byte Type;
    public byte PackedFlags;
    public byte NumModifiers;
    public bool IsByReference;
    public bool IsPinned;
    public int Offset;

    public static ulong ImageBase => PEHelper.ImageBase; 
    
    private string _cachedName;

    public Il2CppType(int offset) {
        var bytes = MetadataContext.Instance.GameAssembly;
        Offset = offset;
    
        if (MetadataConstants.Il2CppTypeDefinitionSize == 8) {
            Data = BitConverter.ToUInt32(bytes, offset);
            Attrs = BitConverter.ToUInt16(bytes, offset + 4);
            Type = bytes[offset + 6];
            PackedFlags = bytes[offset + 7];
        }
        else {
            Data = BitConverter.ToUInt64(bytes, offset);
            Attrs = BitConverter.ToUInt16(bytes, offset + 8);
            Type = bytes[offset + 10];
            PackedFlags = bytes[offset + 11];
        }
    
        (NumModifiers, IsByReference, IsPinned) = DecodePackedFlags(PackedFlags);
    }

    public static (byte NumModifiers, bool IsByReference, bool IsPinned) DecodePackedFlags(byte packedFlags) {
        return (
            (byte)(packedFlags & MetadataConstants.TypeModifiersMask),
            (packedFlags & MetadataConstants.TypeByReferenceMask) != 0,
            (packedFlags & MetadataConstants.TypePinnedMask) != 0
        );
    }

    public static Il2CppType FromIndex(int index) {
        if (index < 0 || index >= MetadataRegistration.Instance.TypeInfoCount) {
            throw new ArgumentOutOfRangeException($"{nameof(index)}, value: {index}"); 
        }
        if (MetadataCache.Types != null && index < MetadataCache.Types.Length && MetadataCache.Types[index] != null)
            return MetadataCache.Types[index];
        int offset = (int)PEHelper.RvaToOffset((uint)(MetadataRegistration.Instance.TypesRva + index * MetadataConstants.Il2CppTypeDefinitionSize));
        
        return new Il2CppType(offset);
    }
    
    public string Name() {
        if (_cachedName != null) return _cachedName;
        _cachedName = ComputeName();
        return _cachedName;
    }

    public string ComputeName() {
        if (PrimitiveTypes.TryGetValue(Type, out var name))
            return name;

        switch (Type) {
            case 0x11:
            case 0x12:
                return ResolveTypeDefName((int)Data);

            case 0x15:
                int genericClassIndex = (int)Data;

                if (GenericClassNameCache.TryGetValue(genericClassIndex, out var cached))
                    return cached;

                int baseOffset = MetadataHeader.Instance.GenericClassOffset +
                    genericClassIndex * MetadataConstants.GenericClassDefinitionSize;
                
                int instIndex = BitConverter.ToInt32(MetadataContext.Instance.StartupMetadata, baseOffset + 4);
                int typeDefIndex = BitConverter.ToInt32(MetadataContext.Instance.StartupMetadata, baseOffset);
                
                string openName = ResolveTypeDefName(typeDefIndex);
                int tick = openName.IndexOf('`');
                if (tick >= 0) openName = openName[..tick];

                if (instIndex == -1)
                    return openName;
                
                int instOffset = (int)PEHelper.RvaToOffset((uint)MetadataRegistration.Instance.GenericInstsOffset) +
                    instIndex * MetadataConstants.GenericInstDefinitionSize;
                int argCount = BitConverter.ToInt32(MetadataContext.Instance.GameAssembly, instOffset);
                long arrayRva = BitConverter.ToInt64(MetadataContext.Instance.GameAssembly, instOffset + 8);

                int arrayOffset = (int)PEHelper.RvaToOffset((uint)((ulong)arrayRva - ImageBase));
                
                var args = new List<string>(argCount);

                for (int i = 0; i < argCount; i++) {
                    long ptr = BitConverter.ToInt64(MetadataContext.Instance.GameAssembly, arrayOffset + i * 8);
                    int typeArgOffset = (int)PEHelper.RvaToOffset((uint)((ulong)ptr - ImageBase));
                    
                    args.Add(new Il2CppType(typeArgOffset).Name());
                }

                string result = $"{openName}<{string.Join(", ", args)}>";
                GenericClassNameCache[genericClassIndex] = result;
                return result;
            
            case 0x0F:
                if (Data == 0) return "void*";
                return ResolveIndirect().Name() + "*";

            case 0x14:
                var (elemOffset, rank) = ResolveArray(); 
                
                return $"{new Il2CppType(elemOffset).Name()}[{new string(',', rank - 1)}]";

            case 0x1D:
                return ResolveIndirect().Name() + "[]";

            case 0x10:
                ulong innerRva = Data - ImageBase;
                ulong innerOffset = PEHelper.RvaToOffset((uint)innerRva);
                return new Il2CppType((int)innerOffset).Name();

            case 0x13:
            case 0x1E:
                int genericParamOffset = MetadataHeader.Instance.GenericParametersOffset +
                    (int)Data * MetadataConstants.GenericParameterDefinitionSize;

                int nameIndex = BitConverter.ToInt32(MetadataContext.Instance.Metadata, genericParamOffset);
                int scramble = (int)(((ulong)(1252900171 *
                            ((((0x617FE3CC452CL * (ulong)Data + 0x9DC5DB71F0EB440L) >> 9)
                              + 718849585)
                             ^ 0x5278374D))) >> 15)
                            + 1149796643;

                int finalIndex = nameIndex - scramble;

                return StringProcessor.Decrypt(finalIndex);

            default:
                Console.WriteLine($"Unsupported type: {Type}, data: {Data:X}, RVA: 0x{PEHelper.OffsetToRva((ulong)Offset):X}");
                return "object";
        }
    }

    public Il2CppType ResolveIndirect() {
        if (MetadataConstants.Il2CppTypeDefinitionSize == 16)
            return new Il2CppType((int)PEHelper.RvaToOffset((uint)(Data - ImageBase)));
        else
            return FromIndex((int)Data);
    }

    public (int, int) ResolveArray() {
        ulong arrayEntryOffset = MetadataConstants.Il2CppTypeDefinitionSize == 16
            ? PEHelper.RvaToOffset((uint)(Data - ImageBase))
            : PEHelper.RvaToOffset((uint)MetadataRegistration.Instance.ArrayOffset + (uint)Data * MetadataConstants.ArrayDefinitionSize);
        long arrayElemPtr = BitConverter.ToInt64(MetadataContext.Instance.GameAssembly, (int)arrayEntryOffset);
        ulong arrayElemOffset = PEHelper.RvaToOffset((uint)((ulong)arrayElemPtr - ImageBase));
        int arrayRank = MetadataContext.Instance.GameAssembly[(int)arrayEntryOffset + MetadataConstants.PointerSize];
        return ((int)arrayElemOffset, arrayRank);
    }

    public static string ResolveTypeDefName(int typeDefinitionIndex) {
        Il2CppTypeDefinition typeDef = new Il2CppTypeDefinition(typeDefinitionIndex);

        string ns = typeDef.Namespace;
        string name = typeDef.Name;

        if (!string.IsNullOrEmpty(ns))
            return ns + "." + name;

        return name;
    }

    public static int DetectTypeSize() {
        byte[] image = MetadataContext.Instance.GameAssembly;
        var registration = MetadataRegistration.Instance;
    
        int count = registration.TypeInfoCount;
        if (count <= 0)
            throw new InvalidDataException("No IL2CPP types found.");
    
        int baseOffset = (int)PEHelper.RvaToOffset((uint)registration.TypesRva);
    
        double score8 = ScoreTypeLayout(image, baseOffset, count, 8, 6);
        double score16 = ScoreTypeLayout(image, baseOffset, count, 16, 10);
    
        if (score8 >= 0.90 && score8 - score16 >= 0.20)
            return 8;
    
        if (score16 >= 0.90 && score16 - score8 >= 0.20)
            return 16;
    
        throw new InvalidDataException($"Could not reliably detect Il2CppType layout " +
            $"(8-byte={score8:P1}, 16-byte={score16:P1}).");
    }
    
    private static double ScoreTypeLayout(byte[] image, int baseOffset, int count, int stride, int typeOffset) {
        int samples = Math.Min(count, 512);
        int matches = 0;
    
        for (int i = 0; i < samples; i++) {
            int index = samples == 1 ? 0 : (int)((long)i * (count - 1) / (samples - 1));
    
            long offset = (long)baseOffset + (long)index * stride + typeOffset;
    
            if (offset >= 0 && offset < image.Length && IsKnownTypeTag(image[(int)offset]))
                matches++;
        }
    
        return (double)matches / samples;
    }
    
    private static bool IsKnownTypeTag(byte type) =>
        type is >= 0x01 and <= 0x16
            or 0x18 or 0x19 or 0x1B
            or 0x1C or 0x1D or 0x1E
            or 0x1F or 0x20 or 0x21
            or 0x40 or 0x41 or 0x45
            or 0x55 or 0xFF;
}
