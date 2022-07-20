using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

using Microsoft.Extensions.Logging;

using MrHotkeys.Linq.LateBinding.Utility;

namespace MrHotkeys.Linq.LateBinding.Dto
{
    public sealed class DtoTypeGenerator : IDtoTypeGenerator
    {
        private ILogger Logger { get; }

        public string DtoAssemblyName { get; private set; }

        private AssemblyBuilder DtoAssemblyBuilder { get; set; }

        private ModuleBuilder DtoModuleBuilder { get; set; }

        public bool UseDtoDictionaryBaseType { get; set; } = true;

        public string? DtoAssemblyNamePrefix { get; set; } = "<DTO_ASSEMBLY>";

        public string? DtoModuleNamePrefix { get; set; } = "<DTO_MODULE>";

        public string? DtoTypeNamePrefix { get; set; } = "<DTO>";

        public string? DtoTypeKeySetFieldNamePrefix { get; set; } = "<DTO_KEYS>";

        public string? DtoTypePropertyNamePrefix { get; set; } = null;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        public DtoTypeGenerator(ILogger<DtoTypeGenerator> logger)
        {
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));

            Reset(true);
        }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

        public void Reset() =>
            Reset(false);

        private void Reset(bool init)
        {
            var oldDtoAssemblyName = DtoAssemblyName;
            var newDtoAssemblyName = $"{DtoAssemblyNamePrefix}{Guid.NewGuid()}";

            if (Logger.IsEnabled(LogLevel.Trace))
            {
                if (init)
                    Logger.LogTrace("Creating DTO Assembly {newDtoAssemblyName}...", newDtoAssemblyName);
                else
                    Logger.LogTrace("Resetting, switching DTO Assembly from {oldDtoAssemblyName} to {newDtoAssemblyName}...", oldDtoAssemblyName, newDtoAssemblyName);
            }

            try
            {
                DtoAssemblyName = newDtoAssemblyName;

                DtoAssemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(
                    name: new AssemblyName(DtoAssemblyName),
                    access: AssemblyBuilderAccess.RunAndCollect);

                DtoModuleBuilder = DtoAssemblyBuilder.DefineDynamicModule($"{DtoModuleNamePrefix}{Guid.NewGuid()}");

                if (Logger.IsEnabled(LogLevel.Debug))
                {
                    if (init)
                        Logger.LogDebug("Successfully created DTO Assembly {newDtoAssemblyName}.", newDtoAssemblyName);
                    else
                        Logger.LogDebug("Successfully switched DTO Assembly from {oldDtoAssemblyName} to {newDtoAssemblyName}.", oldDtoAssemblyName, newDtoAssemblyName);
                }
            }
            catch (Exception e)
            {
                if (init)
                {
                    Logger.LogError(e, "Exception occurred creating DTO Assembly {newDtoAssemblyName}.", newDtoAssemblyName);

                    if (Debugger.IsAttached)
                        throw;

                    throw new InvalidOperationException($"Exception occurred creating DTO Assembly {newDtoAssemblyName}.", e);
                }
                else
                {
                    Logger.LogError(e, "Exception occurred switching DTO Assembly from {oldDtoAssemblyName} to {newDtoAssemblyName}.", oldDtoAssemblyName, newDtoAssemblyName);

                    if (Debugger.IsAttached)
                        throw;

                    throw new InvalidOperationException($"Exception occurred switching DTO Assembly from {oldDtoAssemblyName} to {newDtoAssemblyName}.", e);
                }
            }
        }

        public DtoTypeInfo Generate(IEnumerable<DtoPropertyDefinition> propertyDefinitions)
        {
            if (propertyDefinitions is null)
                throw new ArgumentNullException(nameof(propertyDefinitions));

            var dtoTypeBuilder = ConfigureDtoType(propertyDefinitions, out var propertiesBuilt, out var keySetField);

            var dtoType = CreateDtoType(dtoTypeBuilder);

            if (keySetField is not null)
            {
                // Seed the static set of member names
                var keys = new ReadOnlySetWrapper<string>(propertyDefinitions.Select(d => d.Name).ToHashSet());
                dtoType
                    .GetField(keySetField.Name, BindingFlags.NonPublic | BindingFlags.Static)
                    .SetValue(null, keys);

                if (Logger.IsEnabled(LogLevel.Trace))
                    Logger.LogTrace("Property key set stored in {dtoTypeName}.{keysProperty}: {keys}", dtoTypeBuilder.Name, keySetField.Name, string.Join(", ", keys));
            }

            var selectPropertyMap = propertiesBuilt
                .Select(pair => (Name: pair.Key, Property: dtoType.GetProperty(pair.Value.Name)))
                .ToDictionary(tuple => tuple.Name, tuple => tuple.Property);
            var selectPropertyMapReadOnly = new ReadOnlyDictionary<string, PropertyInfo>(selectPropertyMap);

            var propertyDefinitionsReadOnly = new ReadOnlyCollection<DtoPropertyDefinition>(propertyDefinitions.ToList());

            return new DtoTypeInfo(dtoType, selectPropertyMapReadOnly, propertyDefinitionsReadOnly);
        }

        private TypeBuilder ConfigureDtoType(IEnumerable<DtoPropertyDefinition> propertyDefinitions,
            out Dictionary<string, PropertyBuilder> propertiesBuilt, out FieldBuilder? keySetField)
        {
            // Snapshot members in case another thread touches them
            var dtoTypeName = $"{DtoTypeNamePrefix}{Guid.NewGuid()}";
            var dtoModuleBuilder = DtoModuleBuilder;
            var useDtoDictionaryBaseType = UseDtoDictionaryBaseType;
            var baseType = useDtoDictionaryBaseType ?
                typeof(DtoDictionaryBase) :
                typeof(object);

            if (Logger.IsEnabled(LogLevel.Trace))
            {
                var message = "Configuring DTO Type {dtoTypeName} in Assembly {dtoAssembly} " +
                    (useDtoDictionaryBaseType ? ("using dictionary base " + baseType.FullName) : "using object base") +
                    " for definitions {definitions}...";
                Logger.LogTrace(message, dtoTypeName, dtoModuleBuilder.Assembly.GetName().Name, GetPropertyDefinitionsString(propertyDefinitions));
            }

            try
            {
                var dtoTypeBuilder = dtoModuleBuilder.DefineType(
                    name: dtoTypeName,
                    attr: TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Sealed,
                    parent: baseType
                );

                var dtoPropertyAttributeConstructor = typeof(DtoPropertyAttribute)
                    .GetConstructor(new[] { typeof(string) });

                propertiesBuilt = new Dictionary<string, PropertyBuilder>();
                foreach (var (name, type) in propertyDefinitions)
                {
                    if (string.IsNullOrWhiteSpace(name))
                        throw new ArgumentException("Cannot contain null or empty keys!", nameof(propertyDefinitions));

                    if (propertiesBuilt.ContainsKey(name))
                        throw new ArgumentException($"Contains duplicate property name \"{name}\"!", nameof(propertyDefinitions));

                    var propertyBuilder = EmitHelpers.DefineAutoProperty(dtoTypeBuilder, DtoTypePropertyNamePrefix + name, type, EmitHelpers.Accessibility.Public, EmitHelpers.Accessibility.Public);

                    var attributeBuilder = new CustomAttributeBuilder(dtoPropertyAttributeConstructor, new[] { name });
                    propertyBuilder.SetCustomAttribute(attributeBuilder);

                    propertiesBuilt.Add(name, propertyBuilder);
                }

                if (useDtoDictionaryBaseType)
                {
                    keySetField = BuildPropertyNamesField(dtoTypeBuilder);

                    BuildConstructorForDictionaryBase(dtoTypeBuilder, keySetField);

                    BuildTryGetValue(dtoTypeBuilder, propertiesBuilt);
                    BuildTrySetValue(dtoTypeBuilder, propertiesBuilt);
                }
                else
                {
                    keySetField = null;

                    BuildConstructorForObjectBase(dtoTypeBuilder);
                }

                if (Logger.IsEnabled(LogLevel.Debug))
                    Logger.LogDebug("Successfully configured DTO Type {dtoTypeName} in Assembly {dtoAssembly}.", dtoTypeBuilder.Name, dtoTypeBuilder.Assembly.GetName().Name);

                return dtoTypeBuilder;
            }
            catch (Exception e)
            {
                Logger.LogError(e, "Exception occurred configuring DTO Type {dtoTypeName} in Assembly {dtoAssembly}!", dtoTypeName, dtoModuleBuilder.Assembly.GetName().Name);

                if (Debugger.IsAttached)
                    throw;

                throw new InvalidOperationException($"Exception occurred configuring DTO Type {dtoTypeName} in Assembly {dtoModuleBuilder.Assembly.GetName().Name}!", e);
            }
        }

        private Type CreateDtoType(TypeBuilder dtoTypeBuilder)
        {
            if (Logger.IsEnabled(LogLevel.Trace))
                Logger.LogTrace("Creating DTO Type {dtoTypeName} in Assembly {dtoAssembly}...", dtoTypeBuilder.Name, dtoTypeBuilder.Assembly.GetName().Name);

            try
            {
                var dtoType = dtoTypeBuilder.CreateType()!;

                if (Logger.IsEnabled(LogLevel.Debug))
                    Logger.LogDebug("Successfully created DTO Type {dtoTypeName} in Assembly {dtoAssembly}.", dtoType.Name, dtoType.Assembly.GetName().Name);

                return dtoType;
            }
            catch (Exception e)
            {
                Logger.LogError(e, "Exception occurred creating DTO Type {dtoTypeName} in Assembly {dtoAssembly}!", dtoTypeBuilder.Name, dtoTypeBuilder.Assembly.GetName().Name);

                if (Debugger.IsAttached)
                    throw;
                else
                    throw new InvalidOperationException($"Exception occurred creating DTO Type {dtoTypeBuilder.Name} in Assembly {dtoTypeBuilder.Assembly.GetName().Name}!", e);
            }
        }

        private FieldBuilder BuildPropertyNamesField(TypeBuilder dtoTypeBuilder)
        {
            var fieldName = $"{DtoTypeKeySetFieldNamePrefix}Keys";
            var fieldType = typeof(ReadOnlySetWrapper<string>);

            return TryCatchFieldBuild(dtoTypeBuilder, fieldName, fieldType, () =>
            {
                var fieldBuilder = dtoTypeBuilder.DefineField(
                    fieldName: fieldName,
                    type: typeof(ReadOnlySetWrapper<string>),
                    attributes: FieldAttributes.Private | FieldAttributes.Static);

                return fieldBuilder;
            });
        }

        private ConstructorBuilder BuildConstructorForObjectBase(TypeBuilder dtoTypeBuilder)
        {
            return TryCatchConstructorBuild(dtoTypeBuilder, "object base constructor", () =>
            {
                var constructorBuilder = dtoTypeBuilder.DefineConstructor(
                    attributes: MethodAttributes.Public,
                    callingConvention: CallingConventions.Standard,
                    parameterTypes: Type.EmptyTypes);

                var baseConstructor = typeof(object)
                    .GetConstructor(BindingFlags.Public | BindingFlags.Instance, Type.EmptyTypes)
                    ?? throw new InvalidOperationException();

                var il = constructorBuilder.GetILGenerator();
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Call, baseConstructor);
                il.Emit(OpCodes.Ret);

                return constructorBuilder;
            });
        }

        private ConstructorBuilder BuildConstructorForDictionaryBase(TypeBuilder dtoTypeBuilder, FieldInfo keySetField)
        {
            return TryCatchConstructorBuild(dtoTypeBuilder, "dictionary base constructor", () =>
            {
                var constructorBuilder = dtoTypeBuilder.DefineConstructor(
                    attributes: MethodAttributes.Public,
                    callingConvention: CallingConventions.Standard,
                    parameterTypes: Type.EmptyTypes);

                var baseConstructor = typeof(DtoDictionaryBase)
                    .GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, keySetField.FieldType)
                    ?? throw new InvalidOperationException();

                var il = constructorBuilder.GetILGenerator();
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldarg_0); // Need to load this twice - once for field then once for base ctor
                il.Emit(OpCodes.Ldfld, keySetField);
                il.Emit(OpCodes.Call, baseConstructor);
                il.Emit(OpCodes.Ret);

                return constructorBuilder;
            });
        }

        private MethodBuilder BuildTryGetValue(TypeBuilder dtoTypeBuilder, IDictionary<string, PropertyBuilder> properties)
        {
            var methodName = "TryGetValue";
            var parameterTypes = new[] { typeof(string), typeof(object).MakeByRefType() };
            return TryCatchMethodBuild(dtoTypeBuilder, methodName, parameterTypes, () =>
            {
                var tryGetValueBuilder = dtoTypeBuilder.DefineMethodAndOverride(
                    name: methodName,
                    genericParameterCount: 0,
                    bindingAttr: BindingFlags.Public | BindingFlags.Instance,
                    parameterTypes: parameterTypes
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
                    emitDefaultCallback: il => true
                );
                il.Emit(OpCodes.Ldc_I4_0); // Load false
                il.Emit(OpCodes.Ret);

                return tryGetValueBuilder;
            });
        }

        private MethodBuilder BuildTrySetValue(TypeBuilder dtoTypeBuilder, IDictionary<string, PropertyBuilder> properties)
        {
            var methodName = "TrySetValue";
            var parameterTypes = new[] { typeof(string), typeof(object) };
            return TryCatchMethodBuild(dtoTypeBuilder, methodName, parameterTypes, () =>
            {
                var trySetValueBuilder = dtoTypeBuilder.DefineMethodAndOverride(
                    name: methodName,
                    genericParameterCount: 0,
                    bindingAttr: BindingFlags.Public | BindingFlags.Instance,
                    parameterTypes: parameterTypes
                );

                var il = trySetValueBuilder.GetILGenerator();
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
                    emitDefaultCallback: il => true
                );
                il.Emit(OpCodes.Ldc_I4_0); // Load false
                il.Emit(OpCodes.Ret);

                return trySetValueBuilder;
            });
        }

        private FieldBuilder TryCatchFieldBuild(TypeBuilder dtoTypeBuilder, string fieldName, Type fieldType, Func<FieldBuilder> callback)
        {
            if (Logger.IsEnabled(LogLevel.Trace))
            {
                Logger.LogTrace("Configuring field {fieldType} {fieldName} for DTO Type {dtoTypeName} in Assembly {dtoAssembly}.",
                    fieldType, fieldName, dtoTypeBuilder.Name, dtoTypeBuilder.Assembly.GetName().Name);
            }

            try
            {
                var constructorBuilder = callback();

                if (Logger.IsEnabled(LogLevel.Trace))
                {
                    Logger.LogTrace("Successfully configured field {fieldType} {fieldName} for DTO Type {dtoTypeName} in Assembly {dtoAssembly}.",
                        fieldType, fieldName, dtoTypeBuilder.Name, dtoTypeBuilder.Assembly.GetName().Name);
                }

                return constructorBuilder;
            }
            catch (Exception e)
            {
                Logger.LogError(e, "Exception occurred configuring field {fieldType} {fieldName} for DTO Type {dtoTypeName} in Assembly {dtoAssembly}!",
                    fieldType, fieldName, dtoTypeBuilder.Name, dtoTypeBuilder.Assembly.GetName().Name);

                if (Debugger.IsAttached)
                    throw;

                throw new InvalidOperationException($"Exception occurred configuring field {fieldType} {fieldName} for " +
                    $"DTO Type {dtoTypeBuilder.Name} in Assembly {dtoTypeBuilder.Assembly.GetName().Name}!", e);
            }
        }

        private ConstructorBuilder TryCatchConstructorBuild(TypeBuilder dtoTypeBuilder, string constructorDescription, Func<ConstructorBuilder> callback)
        {
            if (Logger.IsEnabled(LogLevel.Trace))
            {
                Logger.LogTrace("Configuring {constructorDescription} for DTO Type {dtoTypeName} in Assembly {dtoAssembly}.",
                    constructorDescription, dtoTypeBuilder.Name, dtoTypeBuilder.Assembly.GetName().Name);
            }

            try
            {
                var constructorBuilder = callback();

                if (Logger.IsEnabled(LogLevel.Trace))
                {
                    Logger.LogTrace("Successfuly configured {constructorDescription} for DTO Type {dtoTypeName} in Assembly {dtoAssembly}.",
                        constructorDescription, dtoTypeBuilder.Name, dtoTypeBuilder.Assembly.GetName().Name);
                }

                return constructorBuilder;
            }
            catch (Exception e)
            {
                Logger.LogError(e, "Exception occurred configuring {constructorDescription} for DTO Type {dtoTypeName} in Assembly {dtoAssembly}!",
                    constructorDescription, dtoTypeBuilder.Name, dtoTypeBuilder.Assembly.GetName().Name);

                if (Debugger.IsAttached)
                    throw;

                throw new InvalidOperationException($"Exception occurred configuring {constructorDescription} for " +
                    $"DTO Type {dtoTypeBuilder.Name} in Assembly {dtoTypeBuilder.Assembly.GetName().Name}!", e);
            }
        }

        private MethodBuilder TryCatchMethodBuild(TypeBuilder dtoTypeBuilder, string methodName, IReadOnlyList<Type> parameterTypes, Func<MethodBuilder> callback)
        {
            if (Logger.IsEnabled(LogLevel.Trace))
            {
                Logger.LogTrace("Configuring method {methodName}({parameterTypes}) for DTO Type {dtoTypeName} in Assembly {dtoAssembly}.",
                    methodName, GetParameterTypesString(parameterTypes), dtoTypeBuilder.Name, dtoTypeBuilder.Assembly.GetName().Name);
            }

            try
            {
                var methodBuilder = callback();

                if (Logger.IsEnabled(LogLevel.Trace))
                {
                    Logger.LogTrace("Successfully configured method {methodName}({parameterTypes}) for DTO Type {dtoTypeName} in Assembly {dtoAssembly}.",
                        methodName, GetParameterTypesString(parameterTypes), dtoTypeBuilder.Name, dtoTypeBuilder.Assembly.GetName().Name);
                }

                return methodBuilder;
            }
            catch (Exception e)
            {
                Logger.LogError(e, "Exception occurred configuring method {methodName}({parameterTypes}) for DTO Type {dtoTypeName} in Assembly {dtoAssembly}!",
                    methodName, GetParameterTypesString(parameterTypes), dtoTypeBuilder.Name, dtoTypeBuilder.Assembly.GetName().Name);

                if (Debugger.IsAttached)
                    throw;

                throw new InvalidOperationException($"Exception occurred configuring method {methodName}({parameterTypes}) for " +
                    $"DTO Type {dtoTypeBuilder.Name} in Assembly {dtoTypeBuilder.Assembly.GetName().Name}!", e);
            }
        }

        private string GetPropertyDefinitionsString(IEnumerable<DtoPropertyDefinition> propertyDefinitions) =>
            "{ " + string.Join(", ", propertyDefinitions.Select(d => $"{d.Type.Name} {d.Name}")) + " }";

        private string GetParameterTypesString(IReadOnlyList<Type> parameterTypes) =>
            string.Join(", ", parameterTypes.Select(t => t.Name));
    }
}
