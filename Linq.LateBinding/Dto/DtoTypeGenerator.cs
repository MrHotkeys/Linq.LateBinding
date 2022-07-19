using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

using Microsoft.Extensions.Logging;

namespace MrHotkeys.Linq.LateBinding.Dto
{
    public sealed class DtoTypeGenerator : IDtoTypeGenerator
    {
        private ILogger Logger { get; }

        public string DtoAssemblyName { get; set; }

        private AssemblyBuilder DtoAssemblyBuilder { get; set; }

        private ModuleBuilder DtoModuleBuilder { get; set; }

        public bool UseDtoDictionaryBaseType { get; set; } = true;

        public string? SelectMemberPrefix { get; set; }

        public DtoTypeGenerator(ILogger<DtoTypeGenerator> logger)
        {
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
            Reset();
        }

        public void Reset()
        {
            DtoAssemblyName = $"Linq.LateBinding DTO Assembly ({Guid.NewGuid()})";

            DtoAssemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(
                name: new AssemblyName(DtoAssemblyName),
                access: AssemblyBuilderAccess.RunAndCollect);

            DtoModuleBuilder = DtoAssemblyBuilder.DefineDynamicModule("Linq.LateBinding DTO Module");
        }

        public DtoTypeInfo Generate(IEnumerable<DtoPropertyDefinition> propertyDefinitions)
        {
            if (propertyDefinitions is null)
                throw new ArgumentNullException(nameof(propertyDefinitions));

            var dtoTypeBuilder = DtoModuleBuilder.DefineType(
                name: $"DTO ({Guid.NewGuid()})",
                attr: TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Sealed,
                parent: UseDtoDictionaryBaseType ?
                    typeof(DtoDictionaryBase) :
                    null);

            var dtoPropertyAttributeConstructor = typeof(DtoPropertyAttribute)
                .GetConstructor(new[] { typeof(string) });

            var propertiesBuilt = new Dictionary<string, PropertyBuilder>();
            foreach (var (name, type) in propertyDefinitions)
            {
                if (string.IsNullOrWhiteSpace(name))
                    throw new ArgumentException("Cannot contain null or empty keys!", nameof(propertyDefinitions));

                if (propertiesBuilt.ContainsKey(name))
                    throw new ArgumentException($"Contains duplicate property name \"{name}\"!", nameof(propertyDefinitions));

                var propertyBuilder = EmitHelpers.DefineAutoProperty(dtoTypeBuilder, SelectMemberPrefix + name, type, EmitHelpers.Accessibility.Public, EmitHelpers.Accessibility.Public);

                var attributeBuilder = new CustomAttributeBuilder(dtoPropertyAttributeConstructor, new[] { name });
                propertyBuilder.SetCustomAttribute(attributeBuilder);

                propertiesBuilt.Add(name, propertyBuilder);
            }

            Type dtoType;
            if (UseDtoDictionaryBaseType)
            {
                var propertyNamesField = BuildPropertyNamesField(dtoTypeBuilder);
                BuildConstructorForDictionaryBase(dtoTypeBuilder, propertyNamesField);

                BuildTryGetValue(dtoTypeBuilder, propertiesBuilt);
                BuildTrySetValue(dtoTypeBuilder, propertiesBuilt);

                dtoType = dtoTypeBuilder.CreateType()!;

                // Seed the static set of member names
                var keys = new ReadOnlySetWrapper<string>(propertiesBuilt.Keys.ToHashSet());
                dtoType
                    .GetField(propertyNamesField.Name, BindingFlags.NonPublic | BindingFlags.Static)
                    .SetValue(null, keys);
            }
            else
            {
                BuildConstructorForObjectBase(dtoTypeBuilder);

                dtoType = dtoTypeBuilder.CreateType()!;
            }

            var selectPropertyMap = propertiesBuilt
                .Select(pair => (Name: pair.Key, Property: dtoType.GetProperty(pair.Value.Name)))
                .ToDictionary(tuple => tuple.Name, tuple => tuple.Property);
            var selectPropertyMapReadOnly = new ReadOnlyDictionary<string, PropertyInfo>(selectPropertyMap);

            var propertyDefinitionsReadOnly = new ReadOnlyCollection<DtoPropertyDefinition>(propertyDefinitions.ToList());

            return new DtoTypeInfo(dtoType, selectPropertyMapReadOnly, propertyDefinitionsReadOnly);
        }

        private FieldBuilder BuildPropertyNamesField(TypeBuilder dtoTypeBuilder)
        {
            return dtoTypeBuilder.DefineField(
                fieldName: $"DTO_KEYS_PropertyNames",
                type: typeof(ReadOnlySetWrapper<string>),
                attributes: FieldAttributes.Private | FieldAttributes.Static);
        }

        private ConstructorBuilder BuildConstructorForObjectBase(TypeBuilder dtoTypeBuilder)
        {
            var dtoConstructorBuilder = dtoTypeBuilder.DefineConstructor(
                attributes: MethodAttributes.Public,
                callingConvention: CallingConventions.Standard,
                parameterTypes: Type.EmptyTypes);

            var baseConstructor = typeof(object)
                .GetConstructor(BindingFlags.Public | BindingFlags.Instance, Type.EmptyTypes)
                ?? throw new InvalidOperationException();

            var il = dtoConstructorBuilder.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Call, baseConstructor);
            il.Emit(OpCodes.Ret);

            return dtoConstructorBuilder;
        }

        private ConstructorBuilder BuildConstructorForDictionaryBase(TypeBuilder dtoTypeBuilder, FieldInfo propertyNamesField)
        {
            var dtoConstructorBuilder = dtoTypeBuilder.DefineConstructor(
                attributes: MethodAttributes.Public,
                callingConvention: CallingConventions.Standard,
                parameterTypes: Type.EmptyTypes);

            var baseConstructor = typeof(DtoDictionaryBase)
                .GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, propertyNamesField.FieldType)
                ?? throw new InvalidOperationException();

            var il = dtoConstructorBuilder.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldarg_0); // Need to load this twice - once for field then once for base ctor
            il.Emit(OpCodes.Ldfld, propertyNamesField);
            il.Emit(OpCodes.Call, baseConstructor);
            il.Emit(OpCodes.Ret);

            return dtoConstructorBuilder;
        }

        private MethodBuilder BuildTryGetValue(TypeBuilder dtoTypeBuilder, IDictionary<string, PropertyBuilder> properties)
        {
            var tryGetValueBuilder = dtoTypeBuilder.DefineMethodAndOverride(
                name: "TryGetValue",
                genericParameterCount: 0,
                bindingAttr: BindingFlags.Public | BindingFlags.Instance,
                parameterTypes: new[] { typeof(string), typeof(object).MakeByRefType() }
            );

            var il = tryGetValueBuilder.GetILGenerator();
            il.Emit(OpCodes.Ldarg_1);
            EmitHelpers.EmitStringJumpTable(
                il: il,
                caseValues: properties.Keys,
                emitCaseCallback: (il, name) =>
                {
                    var property = properties[name];

                    il.Emit(OpCodes.Ldarg_2);

                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Call, property.GetMethod);

                    if (property.PropertyType.IsValueType)
                        il.Emit(OpCodes.Box, property.PropertyType);

                    il.Emit(OpCodes.Stind_Ref);
                    il.Emit(OpCodes.Ldc_I4_1); // Load true
                    il.Emit(OpCodes.Ret);

                    return false;
                },
                emitDefaultCallback: il => true);
            il.Emit(OpCodes.Ldc_I4_0); // Load false
            il.Emit(OpCodes.Ret);

            return tryGetValueBuilder;
        }

        private MethodBuilder BuildTrySetValue(TypeBuilder dtoTypeBuilder, IDictionary<string, PropertyBuilder> properties)
        {
            var setValueBuilder = dtoTypeBuilder.DefineMethodAndOverride(
                name: $"TrySetValue",
                genericParameterCount: 0,
                bindingAttr: BindingFlags.Public | BindingFlags.Instance,
                parameterTypes: new[] { typeof(string), typeof(object) }
            );

            var il = setValueBuilder.GetILGenerator();
            il.Emit(OpCodes.Ldarg_1);
            EmitHelpers.EmitStringJumpTable(
                il: il,
                caseValues: properties.Keys,
                emitCaseCallback: (il, name) =>
                {
                    var property = properties[name];

                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Ldarg_2);

                    if (property.PropertyType != typeof(object))
                        il.Emit(OpCodes.Unbox_Any, property.PropertyType);

                    il.Emit(OpCodes.Call, property.SetMethod);
                    il.Emit(OpCodes.Ldc_I4_1); // Load true
                    il.Emit(OpCodes.Ret);

                    return false;
                },
                emitDefaultCallback: il => true);
            il.Emit(OpCodes.Ldc_I4_0); // Load false
            il.Emit(OpCodes.Ret);

            return setValueBuilder;
        }
    }
}
