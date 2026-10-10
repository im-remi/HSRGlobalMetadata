using HSRGlobalMetadata.DummyDll.Model;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace HSRGlobalMetadata.DummyDll.Generation;

internal sealed class MetadataDummyAssemblyGenerator : IDisposable {
    private readonly BlankDummyAssemblyGenerator _assemblySet;
    private readonly IDummyMetadataSource _source;
    private readonly TypeDefinition?[] _typeDefinitions;
    private readonly Dictionary<TypeDefinition, int> _typeIndicesByDefinition = [];
    private readonly List<int> _generatedTypeIndices = [];
    private readonly Dictionary<int, GenericParameter> _genericParameters = [];
    private readonly Dictionary<int, DummyGenericParameterModel> _genericParameterModels = [];
    private readonly Dictionary<int, FieldDefinition> _fieldDefinitions = [];
    private readonly Dictionary<int, MethodDefinition> _methodDefinitions = [];
    private readonly Dictionary<int, DummyMethodModel> _methodModels = [];
    private readonly Dictionary<int, PropertyDefinition> _propertyDefinitions = [];
    private readonly Dictionary<int, EventDefinition> _eventDefinitions = [];
    private readonly HashSet<int> _accessorMethodIndices = [];
    private readonly MethodReference _fieldOffsetAttributeConstructor;
    private readonly MethodReference _addressAttributeConstructor;
    private readonly TypeReference _attributeStringType;
    private readonly TypeSystem _typeSystem;

    public IReadOnlyList<AssemblyDefinition> Assemblies => _assemblySet.Assemblies;
    public DummyAssemblyResolver Resolver => _assemblySet.Resolver;
    public DummyDllGenerationReport Report { get; } = new();

    public MetadataDummyAssemblyGenerator(IDummyMetadataSource source) {
        ArgumentNullException.ThrowIfNull(source);
        _source = source;
        _assemblySet = new BlankDummyAssemblyGenerator(source.Images.Select(image => image.Name));
        if (_assemblySet.Assemblies.Count != source.Images.Count + 1) {
            throw new InvalidDataException(
                $"Assembly set contains {_assemblySet.Assemblies.Count - 1} metadata images, " +
                $"but the source exposes {source.Images.Count}."
            );
        }

        AssemblyDefinition template = _assemblySet.Assemblies[0];
        TypeDefinition fieldOffsetAttribute = template.MainModule.Types.Single(type => type.Name == "FieldOffsetAttribute");
        TypeDefinition addressAttribute = template.MainModule.Types.Single(type => type.Name == "AddressAttribute");
        _fieldOffsetAttributeConstructor = fieldOffsetAttribute.Methods.First(method => method.IsConstructor);
        _addressAttributeConstructor = addressAttribute.Methods.First(method => method.IsConstructor);
        _attributeStringType = template.MainModule.TypeSystem.String;
        _typeSystem = template.MainModule.TypeSystem;
        _typeDefinitions = new TypeDefinition?[source.TypeCount];

        CreateTypeSkeletons();
        AttachNestedTypes();
        CreateTypeGenericParameters();
        AddTypeRelationships();
        CreateFields();
        CreateMethodSkeletons();
        AddGenericParameterConstraints();
        PopulateMethodSignatures();
        CreatePropertiesAndAccessors();
        CreateEvents();

        Report.AssemblyCount = Assemblies.Count;
    }

    private void CreateTypeSkeletons() {
        foreach (DummyImageModel image in _source.Images)
            Assemblies[image.Index + 1].MainModule.Types.Clear();

        foreach (DummyImageModel image in _source.Images) {
            for (int index = image.TypeStart; index < image.TypeStart + image.TypeCount; index++) {
                DummyTypeModel sourceType = _source.GetType(index);
                if (sourceType.ImageIndex != image.Index) {
                    throw new InvalidDataException(
                        $"Type definition {index} claims image {sourceType.ImageIndex}, expected {image.Index} ('{image.Name}')."
                    );
                }
                if (_typeDefinitions[index] != null)
                    throw new InvalidDataException($"Type definition {index} was created more than once.");

                var type = new TypeDefinition(
                    sourceType.Namespace,
                    sourceType.Name,
                    sourceType.Attributes
                );
                _typeDefinitions[index] = type;
                _typeIndicesByDefinition.Add(type, index);
                _generatedTypeIndices.Add(index);
                Report.TypeCount++;
            }
        }
    }

    private void AttachNestedTypes() {
        foreach (int index in _generatedTypeIndices) {
            TypeDefinition type = RequireType(index);
            DummyTypeModel sourceType = _source.GetType(index);
            foreach (int nestedIndex in sourceType.NestedTypeIndices) {
                TypeDefinition nestedType = RequireType(nestedIndex);
                if (nestedType.DeclaringType != null && nestedType.DeclaringType != type) {
                    throw new InvalidDataException(
                        $"Nested type definition {nestedIndex} has multiple declaring types: " +
                        $"{nestedType.DeclaringType.FullName} and {type.FullName}."
                    );
                }
                type.NestedTypes.Add(nestedType);
            }
        }

        foreach (int index in _generatedTypeIndices) {
            TypeDefinition type = RequireType(index);
            if (type.DeclaringType != null)
                continue;
            DummyTypeModel sourceType = _source.GetType(index);
            Assemblies[sourceType.ImageIndex + 1].MainModule.Types.Add(type);
        }
    }

    private void CreateTypeGenericParameters() {
        foreach (int index in _generatedTypeIndices) {
            TypeDefinition type = RequireType(index);
            foreach (DummyGenericParameterModel sourceParameter in _source.GetType(index).GenericParameters) {
                if (_genericParameters.ContainsKey(sourceParameter.MetadataIndex)) {
                    throw new InvalidDataException(
                        $"Generic parameter metadata index {sourceParameter.MetadataIndex} is owned more than once."
                    );
                }
                var parameter = new GenericParameter(sourceParameter.Name, type);
                parameter.Attributes = sourceParameter.Attributes;
                type.GenericParameters.Add(parameter);
                _genericParameters.Add(sourceParameter.MetadataIndex, parameter);
                _genericParameterModels.Add(sourceParameter.MetadataIndex, sourceParameter);
            }
        }
    }

    private void AddTypeRelationships() {
        foreach (int index in _generatedTypeIndices) {
            TypeDefinition type = RequireType(index);
            DummyTypeModel sourceType = _source.GetType(index);
            if (sourceType.BaseType != null) {
                type.BaseType = ResolveTypeReference(
                    sourceType.BaseType,
                    type,
                    sourceType,
                    "base type",
                    index
                );
            }
            foreach (DummyTypeSignature sourceInterface in sourceType.Interfaces) {
                TypeReference interfaceType = ResolveTypeReference(
                    sourceInterface,
                    type,
                    sourceType,
                    "interface",
                    index
                );
                type.Interfaces.Add(new InterfaceImplementation(interfaceType));
            }
        }
    }

    private void CreateFields() {
        foreach (int typeIndex in _generatedTypeIndices) {
            TypeDefinition type = RequireType(typeIndex);
            DummyTypeModel sourceType = _source.GetType(typeIndex);
            for (int ordinal = 0; ordinal < sourceType.FieldCount; ordinal++) {
                DummyFieldModel sourceField;
                try {
                    sourceField = _source.GetField(sourceType, ordinal);
                }
                catch (Exception ex) {
                    int fallbackIndex = checked(sourceType.FieldStart + ordinal);
                    AddDiagnostic("Error", "FIELD_SOURCE_INVALID", ex.Message, sourceType, "field", fallbackIndex);
                    var fallback = new FieldDefinition(
                        $"__invalid_field_{fallbackIndex}",
                        FieldAttributes.Private,
                        type.Module.ImportReference(_typeSystem.Object)
                    );
                    type.Fields.Add(fallback);
                    _fieldDefinitions[fallbackIndex] = fallback;
                    Report.FieldCount++;
                    Report.PlaceholderTypeCount++;
                    continue;
                }

                int placeholdersBefore = Report.PlaceholderTypeCount;
                TypeReference fieldType = ResolveTypeReference(
                    sourceField.FieldType,
                    type,
                    sourceType,
                    "field",
                    sourceField.Index
                );
                var field = new FieldDefinition(sourceField.Name, sourceField.Attributes, fieldType);
                type.Fields.Add(field);
                _fieldDefinitions.Add(sourceField.Index, field);
                Report.FieldCount++;
                if (field.IsPublic)
                    Report.PublicFieldCount++;

                bool isSerializableField = field.IsPublic && !field.IsStatic && !field.IsLiteral && !field.IsNotSerialized;
                if (isSerializableField) {
                    Report.SerializableFieldCount++;
                    if (Report.PlaceholderTypeCount != placeholdersBefore)
                        Report.SerializableFieldPlaceholderCount++;
                }
                if (field.IsPublic && Report.PlaceholderTypeCount != placeholdersBefore)
                    Report.PublicMemberPlaceholderCount++;

                if (sourceField.HasConstant)
                    ApplyFieldConstant(sourceField, field, sourceType);
                if (!field.IsLiteral && sourceField.Offset.HasValue)
                    AddFieldOffsetAttribute(type.Module, field, sourceField.Offset.Value);
            }
        }
    }

    private void CreateMethodSkeletons() {
        foreach (int typeIndex in _generatedTypeIndices) {
            TypeDefinition type = RequireType(typeIndex);
            DummyTypeModel sourceType = _source.GetType(typeIndex);
            for (int ordinal = 0; ordinal < sourceType.MethodCount; ordinal++) {
                int methodIndex = checked(sourceType.MethodStart + ordinal);
                DummyMethodModel sourceMethod;
                try {
                    sourceMethod = _source.GetMethod(sourceType, methodIndex);
                }
                catch (Exception ex) {
                    AddDiagnostic("Error", "METHOD_SOURCE_INVALID", ex.Message, sourceType, "method", methodIndex);
                    var fallback = new MethodDefinition(
                        $"__invalid_method_{methodIndex}",
                        MethodAttributes.Private,
                        type.Module.ImportReference(_typeSystem.Void)
                    );
                    type.Methods.Add(fallback);
                    _methodDefinitions[methodIndex] = fallback;
                    Report.MethodCount++;
                    continue;
                }

                var method = new MethodDefinition(
                    sourceMethod.Name,
                    sourceMethod.Attributes,
                    type.Module.ImportReference(_typeSystem.Void)
                ) {
                    ImplAttributes = MethodImplAttributes.IL | MethodImplAttributes.Managed
                };
                type.Methods.Add(method);
                _methodDefinitions.Add(methodIndex, method);
                _methodModels.Add(methodIndex, sourceMethod);
                Report.MethodCount++;
                if (method.IsPublic)
                    Report.PublicMethodCount++;

                foreach (DummyGenericParameterModel sourceParameter in sourceMethod.GenericParameters) {
                    if (_genericParameters.ContainsKey(sourceParameter.MetadataIndex)) {
                        throw new InvalidDataException(
                            $"Generic parameter metadata index {sourceParameter.MetadataIndex} is owned more than once."
                        );
                    }
                    var parameter = new GenericParameter(sourceParameter.Name, method) {
                        Attributes = sourceParameter.Attributes
                    };
                    method.GenericParameters.Add(parameter);
                    _genericParameters.Add(sourceParameter.MetadataIndex, parameter);
                    _genericParameterModels.Add(sourceParameter.MetadataIndex, sourceParameter);
                }
            }
        }
    }

    private void AddGenericParameterConstraints() {
        foreach ((int metadataIndex, GenericParameter parameter) in _genericParameters) {
            if (!_genericParameterModels.TryGetValue(metadataIndex, out DummyGenericParameterModel? sourceParameter))
                continue;
            DummyTypeModel sourceType = GetOwningType(parameter);
            foreach (DummyTypeSignature constraint in sourceParameter.Constraints ?? []) {
                TypeReference constraintType = ResolveTypeReference(
                    constraint,
                    (MemberReference)parameter.Owner,
                    sourceType,
                    "generic constraint",
                    metadataIndex
                );
                parameter.Constraints.Add(new GenericParameterConstraint(constraintType));
                Report.GenericConstraintCount++;
            }
        }
    }

    private DummyTypeModel GetOwningType(GenericParameter parameter) {
        TypeDefinition declaringType = parameter.Owner switch {
            TypeDefinition type => type,
            MethodDefinition method => method.DeclaringType,
            _ => throw new InvalidDataException($"Unsupported generic parameter owner '{parameter.Owner}'.")
        };
        if (!_typeIndicesByDefinition.TryGetValue(declaringType, out int typeIndex))
            throw new InvalidDataException($"Generic parameter owner type '{declaringType.FullName}' has no metadata mapping.");
        return _source.GetType(typeIndex);
    }

    private void PopulateMethodSignatures() {
        foreach ((int methodIndex, DummyMethodModel sourceMethod) in _methodModels) {
            MethodDefinition method = _methodDefinitions[methodIndex];
            if (!_typeIndicesByDefinition.TryGetValue(method.DeclaringType, out int typeIndex))
                throw new InvalidDataException($"Method owner type '{method.DeclaringType.FullName}' has no metadata mapping.");
            DummyTypeModel sourceType = _source.GetType(typeIndex);

            method.ReturnType = ResolveTypeReference(
                sourceMethod.ReturnType,
                method,
                sourceType,
                "method return",
                sourceMethod.Index
            );
            foreach (DummyParameterModel sourceParameter in sourceMethod.Parameters) {
                TypeReference parameterType = ResolveTypeReference(
                    sourceParameter.ParameterType,
                    method,
                    sourceType,
                    "parameter",
                    sourceParameter.Index
                );
                method.Parameters.Add(new ParameterDefinition(
                    sourceParameter.Name,
                    sourceParameter.Attributes,
                    parameterType
                ));
            }

            if (sourceMethod.Address != null) {
                AddAddressAttribute(method.Module, method, sourceMethod.Address);
                Report.AddressedMethodCount++;
            }
            else if (sourceMethod.AddressError != null) {
                AddDiagnostic("Error", "METHOD_ADDRESS_INVALID", sourceMethod.AddressError,
                    sourceType, "method", sourceMethod.Index);
            }

            if (IsDelegateType(method.DeclaringType)) {
                method.ImplAttributes = MethodImplAttributes.Runtime | MethodImplAttributes.Managed;
                method.Body = null;
            }
            else {
                EnsureMinimalBody(method);
            }
        }
    }

    private static bool IsDelegateType(TypeDefinition type) =>
        type.BaseType?.FullName is "System.MulticastDelegate" or "System.Delegate";

    private void CreatePropertiesAndAccessors() {
        foreach (int typeIndex in _generatedTypeIndices) {
            TypeDefinition type = RequireType(typeIndex);
            DummyTypeModel sourceType = _source.GetType(typeIndex);
            for (int ordinal = 0; ordinal < sourceType.PropertyCount; ordinal++) {
                DummyPropertyModel sourceProperty;
                try {
                    sourceProperty = _source.GetProperty(sourceType, ordinal);
                }
                catch (Exception ex) {
                    int propertyIndex = checked(sourceType.PropertyStart + ordinal);
                    AddDiagnostic("Error", "PROPERTY_SOURCE_INVALID", ex.Message, sourceType, "property", propertyIndex);
                    var fallback = new PropertyDefinition(
                        $"__invalid_property_{propertyIndex}",
                        PropertyAttributes.None,
                        type.Module.ImportReference(_typeSystem.Object)
                    );
                    type.Properties.Add(fallback);
                    _propertyDefinitions[propertyIndex] = fallback;
                    Report.PropertyCount++;
                    Report.PlaceholderTypeCount++;
                    continue;
                }

                MethodDefinition? getter = sourceProperty.GetterMethodIndex.HasValue
                    ? GetAccessor(sourceType, sourceProperty.GetterMethodIndex.Value)
                    : null;
                MethodDefinition? setter = sourceProperty.SetterMethodIndex.HasValue
                    ? GetAccessor(sourceType, sourceProperty.SetterMethodIndex.Value)
                    : null;

                TypeReference propertyType = GetPropertyType(sourceProperty, sourceType, getter, setter, type.Module);
                var property = new PropertyDefinition(sourceProperty.Name, PropertyAttributes.None, propertyType) {
                    GetMethod = getter,
                    SetMethod = setter
                };
                type.Properties.Add(property);
                _propertyDefinitions.Add(sourceProperty.Index, property);
                Report.PropertyCount++;
                if (getter?.IsPublic == true || setter?.IsPublic == true)
                    Report.PublicPropertyCount++;

                if (!TryCreateAutomaticPropertyBody(sourceProperty, sourceType, property)) {
                    EnsureMinimalBody(getter);
                    EnsureMinimalBody(setter);
                }
            }
        }
    }

    private void CreateEvents() {
        foreach (int typeIndex in _generatedTypeIndices) {
            TypeDefinition type = RequireType(typeIndex);
            DummyTypeModel sourceType = _source.GetType(typeIndex);
            for (int ordinal = 0; ordinal < sourceType.EventCount; ordinal++) {
                int eventIndex = checked(sourceType.EventStart + ordinal);
                DummyEventModel sourceEvent;
                try {
                    sourceEvent = _source.GetEvent(sourceType, ordinal);
                }
                catch (Exception ex) {
                    AddDiagnostic("Error", "EVENT_SOURCE_INVALID", ex.Message, sourceType, "event", eventIndex);
                    var fallback = new EventDefinition(
                        $"__invalid_event_{eventIndex}",
                        EventAttributes.None,
                        type.Module.ImportReference(_typeSystem.Object)
                    );
                    type.Events.Add(fallback);
                    _eventDefinitions[eventIndex] = fallback;
                    Report.EventCount++;
                    Report.PlaceholderTypeCount++;
                    continue;
                }

                int placeholdersBefore = Report.PlaceholderTypeCount;
                TypeReference eventType = ResolveTypeReference(
                    sourceEvent.EventType,
                    type,
                    sourceType,
                    "event",
                    sourceEvent.Index
                );
                MethodDefinition? addMethod = sourceEvent.AddMethodIndex.HasValue
                    ? GetAccessor(sourceType, sourceEvent.AddMethodIndex.Value)
                    : null;
                MethodDefinition? removeMethod = sourceEvent.RemoveMethodIndex.HasValue
                    ? GetAccessor(sourceType, sourceEvent.RemoveMethodIndex.Value)
                    : null;
                MethodDefinition? invokeMethod = sourceEvent.RaiseMethodIndex.HasValue
                    ? GetAccessor(sourceType, sourceEvent.RaiseMethodIndex.Value)
                    : null;
                var eventDefinition = new EventDefinition(sourceEvent.Name, EventAttributes.None, eventType) {
                    AddMethod = addMethod,
                    RemoveMethod = removeMethod,
                    InvokeMethod = invokeMethod
                };
                type.Events.Add(eventDefinition);
                _eventDefinitions.Add(sourceEvent.Index, eventDefinition);
                Report.EventCount++;
                if (addMethod?.IsPublic == true || removeMethod?.IsPublic == true || invokeMethod?.IsPublic == true) {
                    Report.PublicEventCount++;
                    if (Report.PlaceholderTypeCount != placeholdersBefore)
                        Report.PublicMemberPlaceholderCount++;
                }
            }
        }
    }

    private MethodDefinition GetAccessor(DummyTypeModel sourceType, int methodIndex) {
        if (!_methodDefinitions.TryGetValue(methodIndex, out MethodDefinition? method)) {
            throw new InvalidDataException(
                $"Accessor method {methodIndex} for type definition {sourceType.Index} was not prepared."
            );
        }
        if (_accessorMethodIndices.Add(methodIndex))
            Report.AccessorCount++;
        return method;
    }

    private TypeReference GetPropertyType(
        DummyPropertyModel sourceProperty,
        DummyTypeModel sourceType,
        MethodDefinition? getter,
        MethodDefinition? setter,
        ModuleDefinition module
    ) {
        TypeReference? getterType = getter?.ReturnType;
        TypeReference? setterType = setter?.Parameters.LastOrDefault()?.ParameterType;
        if (setter != null && setterType == null) {
            AddDiagnostic("Error", "PROPERTY_SETTER_HAS_NO_VALUE", "Setter has no value parameter.",
                sourceType, "property", sourceProperty.Index);
        }
        if (getterType != null && setterType != null && getterType.FullName != setterType.FullName) {
            AddDiagnostic("Error", "PROPERTY_ACCESSOR_TYPE_MISMATCH",
                $"Getter type '{getterType.FullName}' does not match setter value type '{setterType.FullName}'.",
                sourceType, "property", sourceProperty.Index);
        }
        return getterType ?? setterType ?? module.ImportReference(_typeSystem.Object);
    }

    private bool TryCreateAutomaticPropertyBody(
        DummyPropertyModel sourceProperty,
        DummyTypeModel sourceType,
        PropertyDefinition property
    ) {
        MethodDefinition? getter = property.GetMethod;
        MethodDefinition? setter = property.SetMethod;
        if (getter == null || setter == null || !getter.HasBody || !setter.HasBody)
            return false;
        if (getter.IsAbstract || setter.IsAbstract || getter.DeclaringType.IsInterface)
            return false;
        if (getter.Parameters.Count != 0 || setter.Parameters.Count != 1)
            return false;
        if (getter.IsStatic != setter.IsStatic)
            return false;
        if (getter.ReturnType.FullName != setter.Parameters[0].ParameterType.FullName)
            return false;

        string backingFieldName = $"<{property.Name}>k__BackingField";
        FieldDefinition? backingField = getter.DeclaringType.Fields.FirstOrDefault(field =>
            field.Name == backingFieldName && field.FieldType.FullName == property.PropertyType.FullName);
        if (backingField == null) {
            FieldAttributes attributes = FieldAttributes.Private;
            if (getter.IsStatic)
                attributes |= FieldAttributes.Static;
            backingField = new FieldDefinition(backingFieldName, attributes, property.PropertyType);
            getter.DeclaringType.Fields.Add(backingField);
            Report.SyntheticBackingFieldCount++;
            AddDiagnostic("Info", "SYNTHETIC_BACKING_FIELD",
                $"Created synthetic backing field '{backingFieldName}'.", sourceType, "property", sourceProperty.Index);
        }
        if (backingField.IsStatic != getter.IsStatic) {
            AddDiagnostic("Error", "BACKING_FIELD_STATIC_MISMATCH",
                $"Backing field '{backingFieldName}' static state does not match its accessors.",
                sourceType, "property", sourceProperty.Index);
            return false;
        }

        getter.Body.Instructions.Clear();
        ILProcessor getterIl = getter.Body.GetILProcessor();
        if (getter.IsStatic) {
            getterIl.Append(getterIl.Create(OpCodes.Ldsfld, backingField));
        }
        else {
            getterIl.Append(getterIl.Create(OpCodes.Ldarg_0));
            getterIl.Append(getterIl.Create(OpCodes.Ldfld, backingField));
        }
        getterIl.Append(getterIl.Create(OpCodes.Ret));

        setter.Body.Instructions.Clear();
        ILProcessor setterIl = setter.Body.GetILProcessor();
        if (setter.IsStatic) {
            setterIl.Append(setterIl.Create(OpCodes.Ldarg_0));
            setterIl.Append(setterIl.Create(OpCodes.Stsfld, backingField));
        }
        else {
            setterIl.Append(setterIl.Create(OpCodes.Ldarg_0));
            setterIl.Append(setterIl.Create(OpCodes.Ldarg_1));
            setterIl.Append(setterIl.Create(OpCodes.Stfld, backingField));
        }
        setterIl.Append(setterIl.Create(OpCodes.Ret));
        return true;
    }

    private static void EnsureMinimalBody(MethodDefinition? method) {
        if (method == null || method.IsAbstract || method.IsPInvokeImpl || method.DeclaringType.IsInterface ||
            method.ImplAttributes.HasFlag(MethodImplAttributes.Runtime) || !method.HasBody ||
            method.Body.Instructions.Count != 0)
            return;

        ILProcessor il = method.Body.GetILProcessor();
        if (method.ReturnType.MetadataType == MetadataType.Void) {
            il.Append(il.Create(OpCodes.Ret));
            return;
        }

        method.Body.InitLocals = true;
        var result = new VariableDefinition(method.ReturnType);
        method.Body.Variables.Add(result);
        il.Append(il.Create(OpCodes.Ldloca, result));
        il.Append(il.Create(OpCodes.Initobj, method.ReturnType));
        il.Append(il.Create(OpCodes.Ldloc, result));
        il.Append(il.Create(OpCodes.Ret));
    }

    private TypeReference ResolveTypeReference(
        DummyTypeSignature signature,
        MemberReference owner,
        DummyTypeModel sourceType,
        string memberKind,
        int memberIndex
    ) {
        ModuleDefinition module = owner.Module;
        switch (signature.Kind) {
            case DummyTypeSignatureKind.Primitive:
                return ResolvePrimitive(module, signature.TypeCode);
            case DummyTypeSignatureKind.TypeDefinition:
                return module.ImportReference(RequireType(signature.DefinitionIndex));
            case DummyTypeSignatureKind.GenericInstance: {
                TypeReference openType = module.ImportReference(RequireType(signature.DefinitionIndex));
                IReadOnlyList<DummyTypeSignature> arguments = signature.GenericArguments ?? [];
                if (arguments.Count == 0)
                    return openType;
                var instance = new GenericInstanceType(openType);
                foreach (DummyTypeSignature argument in arguments) {
                    instance.GenericArguments.Add(ResolveTypeReference(
                        argument,
                        owner,
                        sourceType,
                        memberKind,
                        memberIndex
                    ));
                }
                return instance;
            }
            case DummyTypeSignatureKind.Array:
                return new ArrayType(
                    ResolveElement(signature, owner, sourceType, memberKind, memberIndex),
                    Math.Max(1, signature.Rank)
                );
            case DummyTypeSignatureKind.Pointer:
                return new PointerType(ResolveElement(signature, owner, sourceType, memberKind, memberIndex));
            case DummyTypeSignatureKind.ByReference:
                return new ByReferenceType(ResolveElement(signature, owner, sourceType, memberKind, memberIndex));
            case DummyTypeSignatureKind.TypeGenericParameter:
            case DummyTypeSignatureKind.MethodGenericParameter:
                if (_genericParameters.TryGetValue(signature.GenericParameterIndex, out GenericParameter? parameter))
                    return parameter;
                return Placeholder(signature,
                    $"Generic parameter {signature.GenericParameterIndex} has no generated owner.",
                    sourceType, memberKind, memberIndex, owner, module);
            default:
                return Placeholder(signature, signature.Reason ?? "Unsupported type signature.",
                    sourceType, memberKind, memberIndex, owner, module);
        }
    }

    private TypeReference ResolveElement(
        DummyTypeSignature signature,
        MemberReference owner,
        DummyTypeModel sourceType,
        string memberKind,
        int memberIndex
    ) {
        if (signature.ElementType == null)
            return Placeholder(signature, "Composite type has no element type.",
                sourceType, memberKind, memberIndex, owner, owner.Module);
        return ResolveTypeReference(signature.ElementType, owner, sourceType, memberKind, memberIndex);
    }

    private TypeReference Placeholder(
        DummyTypeSignature signature,
        string reason,
        DummyTypeModel sourceType,
        string memberKind,
        int memberIndex,
        MemberReference? owner,
        ModuleDefinition module
    ) {
        Report.PlaceholderTypeCount++;
        if (owner is MethodDefinition { IsPublic: true })
            Report.PublicMemberPlaceholderCount++;
        AddDiagnostic("Warning", "TYPE_PLACEHOLDER",
            $"Using System.Object for IL2CPP type 0x{signature.TypeCode:X2}: {reason}",
            sourceType, memberKind, memberIndex);
        return module.ImportReference(_typeSystem.Object);
    }

    private TypeReference ResolvePrimitive(ModuleDefinition module, byte typeCode) => module.ImportReference(typeCode switch {
        0x01 => _typeSystem.Void,
        0x02 => _typeSystem.Boolean,
        0x03 => _typeSystem.Char,
        0x04 => _typeSystem.SByte,
        0x05 => _typeSystem.Byte,
        0x06 => _typeSystem.Int16,
        0x07 => _typeSystem.UInt16,
        0x08 => _typeSystem.Int32,
        0x09 => _typeSystem.UInt32,
        0x0A => _typeSystem.Int64,
        0x0B => _typeSystem.UInt64,
        0x0C => _typeSystem.Single,
        0x0D => _typeSystem.Double,
        0x0E => _typeSystem.String,
        0x16 => _typeSystem.TypedReference,
        0x18 => _typeSystem.IntPtr,
        0x19 => _typeSystem.UIntPtr,
        0x1C => _typeSystem.Object,
        _ => throw new NotSupportedException($"Unsupported primitive IL2CPP type code 0x{typeCode:X2}.")
    });

    private void ApplyFieldConstant(
        DummyFieldModel sourceField,
        FieldDefinition field,
        DummyTypeModel sourceType
    ) {
        if (!field.IsLiteral) {
            AddDiagnostic("Warning", "NON_LITERAL_FIELD_CONSTANT",
                "Metadata contains a constant for a field that is not literal.",
                sourceType, "field", sourceField.Index);
            return;
        }
        if (!IsCecilConstant(sourceField.Constant)) {
            AddDiagnostic("Warning", "UNSUPPORTED_FIELD_CONSTANT",
                $"Constant value type '{sourceField.Constant?.GetType().FullName ?? "null"}' is not CLI-serializable.",
                sourceType, "field", sourceField.Index);
            return;
        }
        field.Constant = sourceField.Constant;
    }

    private static bool IsCecilConstant(object? value) => value == null || value is
        bool or char or sbyte or byte or short or ushort or int or uint or long or ulong or float or double or string;

    private void AddFieldOffsetAttribute(ModuleDefinition module, FieldDefinition field, uint offset) {
        var attribute = new CustomAttribute(module.ImportReference(_fieldOffsetAttributeConstructor));
        attribute.Fields.Add(new CustomAttributeNamedArgument(
            "Offset",
            new CustomAttributeArgument(module.ImportReference(_attributeStringType), $"0x{offset:X}")
        ));
        field.CustomAttributes.Add(attribute);
    }

    private void AddAddressAttribute(
        ModuleDefinition module,
        MethodDefinition method,
        DummyMethodAddressModel address
    ) {
        var attribute = new CustomAttribute(module.ImportReference(_addressAttributeConstructor));
        TypeReference stringType = module.ImportReference(_attributeStringType);
        attribute.Fields.Add(new CustomAttributeNamedArgument(
            "RVA",
            new CustomAttributeArgument(stringType, $"0x{address.Rva:X}")
        ));
        attribute.Fields.Add(new CustomAttributeNamedArgument(
            "Offset",
            new CustomAttributeArgument(stringType, $"0x{address.FileOffset:X}")
        ));
        attribute.Fields.Add(new CustomAttributeNamedArgument(
            "VA",
            new CustomAttributeArgument(stringType, $"0x{address.Va:X}")
        ));
        method.CustomAttributes.Add(attribute);
    }

    private TypeDefinition RequireType(int index) {
        if ((uint)index >= (uint)_typeDefinitions.Length || _typeDefinitions[index] == null)
            throw new InvalidDataException($"Type definition {index} has not been generated.");
        return _typeDefinitions[index]!;
    }

    private void AddDiagnostic(
        string severity,
        string code,
        string message,
        DummyTypeModel sourceType,
        string? memberKind = null,
        int? memberIndex = null
    ) {
        Report.Add(new DummyDllDiagnostic(
            severity,
            code,
            message,
            sourceType.ImageIndex,
            sourceType.Index,
            memberKind,
            memberIndex
        ));
    }

    public void Dispose() => _assemblySet.Dispose();
}
