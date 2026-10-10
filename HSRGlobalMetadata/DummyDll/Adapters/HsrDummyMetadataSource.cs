using System.Runtime.InteropServices;
using HSRGlobalMetadata.Configuration;
using HSRGlobalMetadata.DummyDll.Model;
using HSRGlobalMetadata.Structs;
using HSRGlobalMetadata.Structs.Definitions;
using HSRGlobalMetadata.Structs.Runtime;
using HSRGlobalMetadata.Utils;
using Mono.Cecil;

namespace HSRGlobalMetadata.DummyDll.Adapters;

internal sealed class HsrDummyMetadataSource : IDummyMetadataSource {
    private const int MaximumSignatureDepth = 64;

    private readonly DummyImageModel[] _images;
    private readonly int[] _imageByType;
    private readonly Dictionary<int, DummyTypeModel> _types = [];
    private readonly Dictionary<int, DummyMethodModel> _methods = [];

    public int TypeCount => MetadataCache.TypeDefs.Length;
    public IReadOnlyList<DummyImageModel> Images => _images;

    public HsrDummyMetadataSource() {
        _images = new DummyImageModel[MetadataCache.Images.Length];
        _imageByType = Enumerable.Repeat(-1, TypeCount).ToArray();

        for (int imageIndex = 0; imageIndex < MetadataCache.Images.Length; imageIndex++) {
            Il2CppImageDefinition image = MetadataCache.Images[imageIndex];
            ValidateRange(image.TypeStart, image.TypeCount, TypeCount,
                $"image {imageIndex} ('{image.Name}') type range");

            _images[imageIndex] = new DummyImageModel(imageIndex, image.Name, image.TypeStart, image.TypeCount);
            for (int typeIndex = image.TypeStart; typeIndex < image.TypeStart + image.TypeCount; typeIndex++) {
                if (_imageByType[typeIndex] != -1)
                    throw new InvalidDataException($"Type definition {typeIndex} belongs to more than one image.");
                _imageByType[typeIndex] = imageIndex;
            }
        }
    }

    public DummyTypeModel GetType(int typeDefinitionIndex) {
        ValidateIndex(typeDefinitionIndex, TypeCount, "type definition");
        if (_types.TryGetValue(typeDefinitionIndex, out DummyTypeModel? cached))
            return cached;

        int imageIndex = _imageByType[typeDefinitionIndex];
        if (imageIndex < 0)
            throw new InvalidDataException($"Type definition {typeDefinitionIndex} is not covered by any metadata image.");

        Il2CppTypeDefinition source = MetadataCache.TypeDefs[typeDefinitionIndex];
        IReadOnlyList<int> nestedTypes = ReadIndexList(
            MetadataHeader.Instance.NestedTypesOffset,
            source.NestedTypeStart,
            source.NestedTypeCount,
            TypeCount,
            $"type definition {typeDefinitionIndex} nested type"
        );

        var interfaces = new List<DummyTypeSignature>(source.InterfaceCount);
        foreach (int interfaceTypeIndex in ReadIndexList(
                     MetadataHeader.Instance.InterfaceOffset,
                     source.InterfaceStart,
                     source.InterfaceCount,
                     MetadataCache.Types.Length,
                     $"type definition {typeDefinitionIndex} interface type"
                 )) {
            interfaces.Add(DescribeType(MetadataCache.Types[interfaceTypeIndex]));
        }

        var genericParameters = new List<DummyGenericParameterModel>();
        if (source.GenericContainerIndex >= 0) {
            var container = new Il2CppGenericContainerDefinition(source.GenericContainerIndex);
            for (int ordinal = 0; ordinal < container.Parameters.Count; ordinal++) {
                genericParameters.Add(DescribeGenericParameter(container, ordinal));
            }
        }

        DummyTypeSignature? baseType = source.ParentIndex >= 0 ? DescribeType(source.Parent) : null;
        var model = new DummyTypeModel(
            typeDefinitionIndex,
            imageIndex,
            source.Namespace,
            source.Name,
            (Mono.Cecil.TypeAttributes)(uint)source.Flags,
            source.DeclaringTypeIndex,
            nestedTypes,
            baseType,
            interfaces,
            genericParameters,
            source.FieldStart,
            source.FieldCount,
            source.MethodStart,
            source.MethodCount,
            source.PropertyStart,
            source.PropertyCount,
            source.EventStart,
            source.EventCount
        );
        _types.Add(typeDefinitionIndex, model);
        return model;
    }

    public DummyFieldModel GetField(DummyTypeModel declaringType, int fieldOrdinal) {
        ArgumentNullException.ThrowIfNull(declaringType);
        ValidateIndex(fieldOrdinal, declaringType.FieldCount,
            $"field ordinal for type definition {declaringType.Index}");

        int fieldIndex = checked(declaringType.FieldStart + fieldOrdinal);
        int fieldOffsetIndex = MetadataCache.TypeDefs[declaringType.Index].GetFieldOffsetIndex();
        var source = new Il2CppFieldDefinition(declaringType.FieldStart, fieldOffsetIndex, fieldIndex);
        bool hasConstant = source.TryGetFieldStaticValue(out object? constant);

        return new DummyFieldModel(
            fieldIndex,
            source.Name,
            (Mono.Cecil.FieldAttributes)source.Type.Attrs,
            DescribeType(source.Type),
            source.Offset,
            hasConstant,
            constant
        );
    }

    public DummyMethodModel GetMethod(DummyTypeModel declaringType, int absoluteMethodIndex) {
        ArgumentNullException.ThrowIfNull(declaringType);
        ValidateRange(declaringType.MethodStart, declaringType.MethodCount, int.MaxValue,
            $"type definition {declaringType.Index} method range");
        if (absoluteMethodIndex < declaringType.MethodStart ||
            absoluteMethodIndex >= declaringType.MethodStart + declaringType.MethodCount) {
            throw new ArgumentOutOfRangeException(nameof(absoluteMethodIndex),
                $"Method {absoluteMethodIndex} is outside type definition {declaringType.Index} range " +
                $"[{declaringType.MethodStart}, {declaringType.MethodStart + declaringType.MethodCount}).");
        }
        if (_methods.TryGetValue(absoluteMethodIndex, out DummyMethodModel? cached))
            return cached;

        var source = new Il2CppMethodDefinition(absoluteMethodIndex);
        var genericParameters = new List<DummyGenericParameterModel>();
        if (source.GenericContainerIndex >= 0) {
            var container = new Il2CppGenericContainerDefinition(source.GenericContainerIndex);
            for (int ordinal = 0; ordinal < container.Parameters.Count; ordinal++) {
                genericParameters.Add(DescribeGenericParameter(container, ordinal));
            }
        }

        var parameters = new List<DummyParameterModel>(source.ParametersCount);
        for (int ordinal = 0; ordinal < source.ParametersCount; ordinal++) {
            int parameterIndex = checked(source.ParametersStart + ordinal);
            var parameter = new Il2CppParameterDefinition(parameterIndex);
            parameters.Add(new DummyParameterModel(
                parameterIndex,
                parameter.Name,
                (ParameterAttributes)parameter.Type.Attrs,
                DescribeType(parameter.Type)
            ));
        }

        DummyMethodAddressModel? address = DescribeMethodAddress(source.MethodPointer, out string? addressError);
        var model = new DummyMethodModel(
            absoluteMethodIndex,
            source.Name,
            (Mono.Cecil.MethodAttributes)source.Flags,
            DescribeType(source.ReturnType),
            genericParameters,
            parameters,
            address,
            addressError
        );
        _methods.Add(absoluteMethodIndex, model);
        return model;
    }

    public DummyPropertyModel GetProperty(DummyTypeModel declaringType, int propertyOrdinal) {
        ArgumentNullException.ThrowIfNull(declaringType);
        ValidateIndex(propertyOrdinal, declaringType.PropertyCount,
            $"property ordinal for type definition {declaringType.Index}");

        int propertyIndex = checked(declaringType.PropertyStart + propertyOrdinal);
        var source = new Il2CppPropertyDefinition(propertyIndex);
        int? getter = ResolveAccessorIndex(source.GetMethodIndex, declaringType, propertyIndex, "getter");
        int? setter = ResolveAccessorIndex(source.SetMethodIndex, declaringType, propertyIndex, "setter");
        return new DummyPropertyModel(propertyIndex, source.Name, getter, setter);
    }

    public DummyEventModel GetEvent(DummyTypeModel declaringType, int eventOrdinal) {
        ArgumentNullException.ThrowIfNull(declaringType);
        ValidateIndex(eventOrdinal, declaringType.EventCount,
            $"event ordinal for type definition {declaringType.Index}");

        int eventIndex = checked(declaringType.EventStart + eventOrdinal);
        var source = new Il2CppEventDefinition(eventIndex);
        ValidateIndex(source.TypeIndex, MetadataCache.Types.Length, $"event {eventIndex} type");
        return new DummyEventModel(
            eventIndex,
            source.Name,
            DescribeType(MetadataCache.Types[source.TypeIndex]),
            ResolveAccessorIndex(source.AddMethodIndex, declaringType, eventIndex, "event", "add"),
            ResolveAccessorIndex(source.RemoveMethodIndex, declaringType, eventIndex, "event", "remove"),
            ResolveAccessorIndex(source.RaiseMethodIndex, declaringType, eventIndex, "event", "raise")
        );
    }

    private DummyGenericParameterModel DescribeGenericParameter(
        Il2CppGenericContainerDefinition container,
        int ordinal
    ) {
        Il2CppGenericParameterDefinition parameter = container.Parameters[ordinal];
        var constraints = new List<DummyTypeSignature>(parameter.ConstraintsCount);
        for (int constraintOrdinal = 0; constraintOrdinal < parameter.ConstraintsCount; constraintOrdinal++) {
            int constraintIndex = checked(parameter.ConstraintsStart + constraintOrdinal);
            var constraint = new Il2CppGenericParameterConstraintDefinition(constraintIndex);
            if (constraint.ConstraintIndex < 0)
                continue;
            ValidateIndex(constraint.ConstraintIndex, MetadataCache.Types.Length,
                $"generic parameter {container.GenericParameterStart + ordinal} constraint");
            constraints.Add(DescribeType(MetadataCache.Types[constraint.ConstraintIndex]));
        }
        return new DummyGenericParameterModel(
            checked(container.GenericParameterStart + ordinal),
            parameter.Name,
            GenericParameterAttributes.NonVariant,
            constraints
        );
    }

    private static DummyMethodAddressModel? DescribeMethodAddress(long methodPointer, out string? error) {
        error = null;
        if (methodPointer == 0)
            return null;
        if (methodPointer < 0) {
            error = $"Method pointer 0x{unchecked((ulong)methodPointer):X} exceeds Int64 virtual-address range.";
            return null;
        }

        ulong va = (ulong)methodPointer;
        ulong imageBase = PEHelper.ImageBase;
        if (va < imageBase) {
            error = $"Method pointer 0x{va:X} is below ImageBase 0x{imageBase:X}.";
            return null;
        }
        ulong rva = va - imageBase;
        if (rva > uint.MaxValue) {
            error = $"Method RVA 0x{rva:X} exceeds the supported PE range.";
            return null;
        }
        ulong fileOffset = PEHelper.RvaToOffset((uint)rva);
        if (fileOffset == ulong.MaxValue) {
            error = $"Method RVA 0x{rva:X} is outside all PE sections.";
            return null;
        }
        return new DummyMethodAddressModel(va, rva, fileOffset);
    }

    private DummyTypeSignature DescribeType(Il2CppType type) =>
        DescribeType(type, new HashSet<int>(), 0);

    private DummyTypeSignature DescribeType(Il2CppType type, HashSet<int> activeOffsets, int depth) {
        if (depth > MaximumSignatureDepth)
            return DummyTypeSignature.Unsupported(type.Type, "Type signature nesting exceeds 64 levels.");
        if (!activeOffsets.Add(type.Offset))
            return DummyTypeSignature.Unsupported(type.Type, $"Recursive type signature at GameAssembly offset 0x{type.Offset:X}.");

        try {
            DummyTypeSignature signature;
            if (type.Type is >= 0x01 and <= 0x0E or 0x16 or 0x18 or 0x19 or 0x1C) {
                signature = DummyTypeSignature.Primitive(type.Type);
            }
            else signature = type.Type switch {
                0x0F => DummyTypeSignature.Pointer(DescribeIndirectType(type, activeOffsets, depth)),
                0x10 => DummyTypeSignature.ByReference(DescribeIndirectType(type, activeOffsets, depth)),
                0x11 or 0x12 => DescribeDefinition(type),
                0x13 => DummyTypeSignature.GenericParameter(type.Type, checked((int)type.Data), method: false),
                0x14 => DescribeArray(type, activeOffsets, depth),
                0x15 => DescribeGenericInstance(type, activeOffsets, depth),
                0x1D => DummyTypeSignature.Array(type.Type, DescribeIndirectType(type, activeOffsets, depth)),
                0x1E => DummyTypeSignature.GenericParameter(type.Type, checked((int)type.Data), method: true),
                _ => DummyTypeSignature.Unsupported(type.Type,
                    $"Unsupported IL2CPP type code 0x{type.Type:X2}, data 0x{type.Data:X}.")
            };
            return type.IsByReference && signature.Kind != DummyTypeSignatureKind.ByReference
                ? DummyTypeSignature.ByReference(signature)
                : signature;
        }
        catch (Exception ex) when (ex is ArgumentException or ArithmeticException or InvalidDataException) {
            return DummyTypeSignature.Unsupported(type.Type,
                $"Invalid IL2CPP type code 0x{type.Type:X2}, data 0x{type.Data:X}: {ex.Message}");
        }
        finally {
            activeOffsets.Remove(type.Offset);
        }
    }

    private DummyTypeSignature DescribeIndirectType(Il2CppType type, HashSet<int> activeOffsets, int depth) {
        if (type.Data == 0 && type.Type == 0x0F)
            return DummyTypeSignature.Primitive(0x01);
        return DescribeType(type.ResolveIndirect(), activeOffsets, depth + 1);
    } 

    private DummyTypeSignature DescribeDefinition(Il2CppType type) {
        int definitionIndex = checked((int)type.Data);
        ValidateIndex(definitionIndex, TypeCount, "type definition referenced by IL2CPP type");
        return DummyTypeSignature.Definition(type.Type, definitionIndex);
    }

    private DummyTypeSignature DescribeIndirect(ulong virtualAddress, HashSet<int> activeOffsets, int depth) {
        int offset = VirtualAddressToOffset(virtualAddress);
        return DescribeType(new Il2CppType(offset), activeOffsets, depth + 1);
    }

    private DummyTypeSignature DescribeArray(Il2CppType type, HashSet<int> activeOffsets, int depth) {
        (int elementOffset, int rank) = type.ResolveArray();
        return DummyTypeSignature.Array(
            type.Type,
            DescribeType(new Il2CppType(elementOffset), activeOffsets, depth + 1),
            rank
        );
    }

    private DummyTypeSignature DescribeGenericInstance(
        Il2CppType type,
        HashSet<int> activeOffsets,
        int depth
    ) {
        int genericClassIndex = checked((int)type.Data);
        int classOffset = checked(MetadataHeader.Instance.GenericClassOffset +
            genericClassIndex * MetadataConstants.GenericClassDefinitionSize);
        byte[] startup = MetadataContext.Instance.StartupMetadata;
        EnsureReadable(startup, classOffset, 8, "generic class descriptor");
        int definitionIndex = BitConverter.ToInt32(startup, classOffset);
        int instanceIndex = BitConverter.ToInt32(startup, classOffset + 4);
        ValidateIndex(definitionIndex, TypeCount, "generic type definition");

        if (instanceIndex < 0)
            return DummyTypeSignature.GenericInstance(definitionIndex, []);

        int instanceBase = checked((int)PEHelper.RvaToOffset((uint)MetadataRegistration.Instance.GenericInstsOffset));
        int instanceOffset = checked(instanceBase +
            instanceIndex * MetadataConstants.GenericInstDefinitionSize);
        byte[] gameAssembly = MetadataContext.Instance.GameAssembly;
        EnsureReadable(gameAssembly, instanceOffset, 16, "generic instance descriptor");
        int argumentCount = BitConverter.ToInt32(gameAssembly, instanceOffset);
        ulong argumentArrayPointer = BitConverter.ToUInt64(gameAssembly, instanceOffset + 8);
        if (argumentCount < 0 || argumentCount > 1024)
            throw new InvalidDataException($"Generic instance {instanceIndex} has invalid argument count {argumentCount}.");

        int argumentArrayOffset = VirtualAddressToOffset(argumentArrayPointer);
        EnsureReadable(gameAssembly, argumentArrayOffset,
            checked(argumentCount * MetadataConstants.PointerSize),
            "generic argument pointer array");

        var arguments = new List<DummyTypeSignature>(argumentCount);
        for (int ordinal = 0; ordinal < argumentCount; ordinal++) {
            ulong argumentPointer = BitConverter.ToUInt64(gameAssembly,
                argumentArrayOffset + ordinal * MetadataConstants.PointerSize);
            arguments.Add(DescribeIndirect(argumentPointer, activeOffsets, depth));
        }
        return DummyTypeSignature.GenericInstance(definitionIndex, arguments);
    }

    private static IReadOnlyList<int> ReadIndexList(
        int tableOffset,
        int start,
        int count,
        int targetCount,
        string description
    ) {
        if (count == 0)
            return [];
        if (start < 0 || count < 0)
            throw new InvalidDataException($"{description} range [{start}, {start + count}) is invalid.");

        int indexSize = MetadataConstants.IndexSize;
        int byteOffset = checked(tableOffset + start * indexSize);
        int byteCount = checked(count * indexSize);
        byte[] metadata = MetadataContext.Instance.Metadata;
        EnsureReadable(metadata, byteOffset, byteCount, description);

        var result = new int[count];
        ReadOnlySpan<byte> span = metadata.AsSpan(byteOffset, byteCount);
        for (int ordinal = 0; ordinal < count; ordinal++) {
            int index = MemoryMarshal.Read<int>(span.Slice(ordinal * indexSize, indexSize));
            ValidateIndex(index, targetCount, description);
            result[ordinal] = index;
        }
        return result;
    }

    private static int? ResolveAccessorIndex(
        int relativeIndex,
        DummyTypeModel declaringType,
        int propertyIndex,
        string accessorKind
    ) => ResolveAccessorIndex(relativeIndex, declaringType, propertyIndex, "property", accessorKind);

    private static int? ResolveAccessorIndex(
        int relativeIndex,
        DummyTypeModel declaringType,
        int memberIndex,
        string memberKind,
        string accessorKind
    ) {
        if (relativeIndex < 0)
            return null;
        if (relativeIndex >= declaringType.MethodCount) {
            throw new InvalidDataException(
                $"{memberKind} {memberIndex} {accessorKind} index {relativeIndex} is outside type definition " +
                $"{declaringType.Index} method range [0, {declaringType.MethodCount})."
            );
        }
        return checked(declaringType.MethodStart + relativeIndex);
    }

    private static int VirtualAddressToOffset(ulong virtualAddress) {
        ulong imageBase = PEHelper.ImageBase;
        if (virtualAddress < imageBase)
            throw new InvalidDataException($"Virtual address 0x{virtualAddress:X} is below ImageBase 0x{imageBase:X}.");
        ulong rva = virtualAddress - imageBase;
        if (rva > uint.MaxValue)
            throw new InvalidDataException($"RVA 0x{rva:X} exceeds the supported PE range.");
        ulong offset = PEHelper.RvaToOffset((uint)rva);
        if (offset > int.MaxValue)
            throw new InvalidDataException($"File offset 0x{offset:X} exceeds Int32.MaxValue.");
        return (int)offset;
    }

    private static void ValidateIndex(int index, int count, string description) {
        if ((uint)index >= (uint)count)
            throw new ArgumentOutOfRangeException(nameof(index),
                $"{description} index {index} is outside [0, {count}).");
    }

    private static void ValidateRange(int start, int count, int total, string description) {
        if (start < 0 || count < 0 || start > total - count)
            throw new ArgumentOutOfRangeException(nameof(start),
                $"{description} [{start}, {start + (long)count}) is outside [0, {total}).");
    }

    private static void EnsureReadable(byte[] bytes, int offset, int count, string description) {
        if (offset < 0 || count < 0 || offset > bytes.Length - count)
            throw new InvalidDataException(
                $"{description} byte range [0x{offset:X}, 0x{offset + (long)count:X}) exceeds buffer length 0x{bytes.Length:X}."
            );
    }
}
