using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace MrHotkeys.Linq.LateBinding
{
    public sealed class DtoTypeGenerator : IDtoTypeGenerator
    {
        public string DtoAssemblyName { get; }

        private AssemblyBuilder DtoAssemblyBuilder { get; }

        private ModuleBuilder DtoModuleBuilder { get; }

        public DtoTypeGenerator()
            : this($"Linq.LateBinding DTO Assembly ({Guid.NewGuid()})")
        { }

        public DtoTypeGenerator(string assemblyName)
        {
            DtoAssemblyName = assemblyName ?? throw new ArgumentNullException(nameof(assemblyName));

            DtoAssemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(
                name: new AssemblyName(assemblyName),
                access: AssemblyBuilderAccess.RunAndCollect);

            DtoModuleBuilder = DtoAssemblyBuilder.DefineDynamicModule("StitchEF DTO Module");
        }

        public Type Generate(IEnumerable<DtoPropertyDefinition> propertyDefinitions)
        {
            if (propertyDefinitions is null)
                throw new ArgumentNullException(nameof(propertyDefinitions));

            var dtoTypeBuilder = DtoModuleBuilder.DefineType(
                name: $"DTO ({Guid.NewGuid()})",
                attr: TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Sealed);

            BuildNoParamConstructor(dtoTypeBuilder);

            var targetPropertiesBuilt = new HashSet<string>();
            foreach (var propertyDefinition in propertyDefinitions)
            {
                if (string.IsNullOrWhiteSpace(propertyDefinition.Name))
                    throw new ArgumentException("Cannot contain null or empty keys!", nameof(propertyDefinitions));

                if (!targetPropertiesBuilt.Add(propertyDefinition.Name))
                    throw new ArgumentException($"Contains duplicate property name \"{propertyDefinition.Name}\"!", nameof(propertyDefinitions));

                var property = BuildProperty(propertyDefinition.Name, propertyDefinition.Type, dtoTypeBuilder);
            }

            return dtoTypeBuilder.CreateType()!;
        }

        private ConstructorBuilder BuildNoParamConstructor(TypeBuilder dtoTypeBuilder)
        {
            var dtoConstructorBuilder = dtoTypeBuilder.DefineConstructor(
                attributes: MethodAttributes.Public,
                callingConvention: CallingConventions.Standard,
                parameterTypes: Type.EmptyTypes);

            var il = dtoConstructorBuilder.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Call, typeof(object).GetConstructor(Type.EmptyTypes)!);
            il.Emit(OpCodes.Ret);

            return dtoConstructorBuilder;
        }

        private PropertyInfo BuildProperty(string name, Type type, TypeBuilder dtoTypeBuilder)
        {
            var backingFieldBuilder = dtoTypeBuilder.DefineField(
                    fieldName: $"<{name}>k__BackingField",
                    type: type,
                    attributes: FieldAttributes.Private);

            var propertyBuilder = dtoTypeBuilder.DefineProperty(
                name: name,
                attributes: PropertyAttributes.None,
                returnType: type,
                parameterTypes: Type.EmptyTypes);

            // Getters and setters are always implemented on the actual DTO type
            BuildGetter(name, type, dtoTypeBuilder, backingFieldBuilder, propertyBuilder);
            BuildSetter(name, type, dtoTypeBuilder, backingFieldBuilder, propertyBuilder);

            return propertyBuilder;
        }

        private void BuildGetter(string name, Type type, TypeBuilder dtoTypeBuilder,
            FieldBuilder backingFieldBuilder, PropertyBuilder propertyBuilder)
        {
            var getterBuilder = dtoTypeBuilder.DefineMethod(
                name: $"get_{propertyBuilder.Name}",
                attributes: MethodAttributes.Public | MethodAttributes.Virtual,
                returnType: type,
                parameterTypes: Type.EmptyTypes);

            var il = getterBuilder.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldfld, backingFieldBuilder);
            il.Emit(OpCodes.Ret);

            propertyBuilder.SetGetMethod(getterBuilder);
        }

        private void BuildSetter(string name, Type type, TypeBuilder dtoTypeBuilder,
            FieldBuilder backingFieldBuilder, PropertyBuilder propertyBuilder)
        {
            var setterBuilder = dtoTypeBuilder.DefineMethod(
                name: $"set_{propertyBuilder.Name}",
                attributes: MethodAttributes.Public | MethodAttributes.Virtual,
                returnType: null,
                parameterTypes: new[] { type });

            var il = setterBuilder.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Stfld, backingFieldBuilder);
            il.Emit(OpCodes.Ret);

            propertyBuilder.SetSetMethod(setterBuilder);
        }
    }
}
