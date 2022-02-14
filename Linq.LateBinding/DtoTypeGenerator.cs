using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

namespace MrHotkeys.Linq.LateBinding
{
    public sealed class DtoTypeGenerator : IDtoTypeGenerator
    {
        public string DtoAssemblyName { get; }

        private AssemblyBuilder DtoAssemblyBuilder { get; }

        private ModuleBuilder DtoModuleBuilder { get; }

        public DtoTypeGenerator()
            : this($"StitchEF DTO Assembly ({Guid.NewGuid()})")
        { }

        public DtoTypeGenerator(string assemblyName)
        {
            DtoAssemblyName = assemblyName ?? throw new ArgumentNullException(nameof(assemblyName));

            DtoAssemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(
                name: new AssemblyName(assemblyName),
                access: AssemblyBuilderAccess.RunAndCollect);

            DtoModuleBuilder = DtoAssemblyBuilder.DefineDynamicModule("StitchEF DTO Module");
        }

        public Type Generate<TSource, TDto>(ICollection<string> propertyNames)
        {
            if (propertyNames is null)
                throw new ArgumentNullException(nameof(propertyNames));
            if (!typeof(TDto).IsInterface)
                throw new ArgumentException($"Cannot create DTO for non-interface type {typeof(TDto).Name}!", nameof(TDto));

            // We need to make sure that we can access the types, since they're coming from other assemblies
            EnsureCanAccess<TSource>(nameof(TSource));
            EnsureCanAccess<TDto>(nameof(TDto));

            var dtoTypeBuilder = DtoModuleBuilder.DefineType(
                name: $"{typeof(TSource).Name} to {typeof(TDto).Name} DTO ({Guid.NewGuid()})",
                attr: TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Sealed);

            dtoTypeBuilder.AddInterfaceImplementation(typeof(TDto));

            BuildNoParamConstructor(dtoTypeBuilder);
            var dtoConstructorBuilderIL = BuildCopyConstructor<TSource>(dtoTypeBuilder);

            var targetPropertiesBuilt = new HashSet<PropertyInfo>();
            foreach (var propertyName in propertyNames)
            {
                if (string.IsNullOrWhiteSpace(propertyName))
                    throw new ArgumentException("Cannot contain null or empty!", nameof(propertyNames));

                var sourceProperty = typeof(TSource).GetProperty(propertyName);
                if (sourceProperty is null)
                    throw new ArgumentException($"Property {propertyName} not found on type {typeof(TSource).Name}!", nameof(propertyNames));

                var targetProperty = typeof(TDto).GetProperty(propertyName);
                if (targetProperty is null)
                    throw new ArgumentException($"Property {propertyName} not found on type {typeof(TDto).Name}!", nameof(propertyNames));

                if (!targetPropertiesBuilt.Add(targetProperty))
                    throw new ArgumentException($"Contains duplicate property name \"{propertyName}\"!", nameof(propertyNames));

                // Index parameters are not supported
                if (sourceProperty.GetIndexParameters().Length > 0)
                    throw new ArgumentException($"Cannot map property {propertyName} with index parameters found on type {typeof(TSource).Name}!", nameof(propertyNames));
                if (targetProperty.GetIndexParameters().Length > 0)
                    throw new ArgumentException($"Cannot map property {propertyName} with index parameters found on type {typeof(TDto).Name}!", nameof(propertyNames));

                if (!sourceProperty.PropertyType.IsAssignableTo(targetProperty.PropertyType))
                {
                    throw new ArgumentException($"Cannot map property {propertyName} with incompatible types between {typeof(TSource).Name} " +
                        $"({sourceProperty.PropertyType.Name}) and {typeof(TDto).Name} ({targetProperty.PropertyType.Name})!", nameof(propertyNames));
                }

                BuildProperty(sourceProperty, targetProperty, dtoTypeBuilder, dtoConstructorBuilderIL);

                targetPropertiesBuilt.Add(targetProperty);
            }

            // Any unimplemented property defined by the DTO interface needs a stub implementation to satisfy the compiler
            // We'll implement with one that just throws a System.NotImplementedException
            foreach (var targetProperty in typeof(TDto).GetProperties().Except(targetPropertiesBuilt))
                BuildPropertyStub(targetProperty, dtoTypeBuilder);

            EmitCopyConstructorEndIL(dtoConstructorBuilderIL);

            return dtoTypeBuilder.CreateType()!;
        }

        private void BuildNoParamConstructor(TypeBuilder dtoTypeBuilder)
        {
            var dtoConstructorBuilder = dtoTypeBuilder.DefineConstructor(
                attributes: MethodAttributes.Public,
                callingConvention: CallingConventions.Standard,
                parameterTypes: Type.EmptyTypes);

            EmitNoParamConstructorIL(dtoConstructorBuilder.GetILGenerator());
        }

        private void EmitNoParamConstructorIL(ILGenerator il)
        {
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Call, typeof(object).GetConstructor(Type.EmptyTypes)!);
            il.Emit(OpCodes.Ret);
        }

        private ILGenerator BuildCopyConstructor<TSource>(TypeBuilder dtoTypeBuilder)
        {
            var constructorBuilder = dtoTypeBuilder.DefineConstructor(
                attributes: MethodAttributes.Public,
                callingConvention: CallingConventions.Standard,
                parameterTypes: new[] { typeof(TSource) });
            constructorBuilder.DefineParameter(1, ParameterAttributes.None, "source");

            var il = constructorBuilder.GetILGenerator();

            EmitCopyConstructorStartIL(il);

            return il;
        }

        private void EmitCopyConstructorStartIL(ILGenerator il)
        {
            // Call base constructor on System.Object
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Call, typeof(object).GetConstructor(Type.EmptyTypes)!);

            // Make sure the source object we were given is not null
            var postCheckLabel = il.DefineLabel();
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Brtrue_S, postCheckLabel); // Will branch if the stack has a non-null value at the top
            il.Emit(OpCodes.Ldstr, "source");
            il.Emit(OpCodes.Ldstr, "Source object may not be null!");
            il.Emit(OpCodes.Newobj, typeof(ArgumentNullException).GetConstructor(new[] { typeof(string), typeof(string) })!);
            il.Emit(OpCodes.Throw);
            il.MarkLabel(postCheckLabel);
        }

        private void EmitCopyConstructorPropertyCopyIL(ILGenerator il, MethodInfo sourcePropertyGetter, FieldInfo dtoBackingField)
        {
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Callvirt, sourcePropertyGetter);
            il.Emit(OpCodes.Stfld, dtoBackingField);
        }

        private void EmitCopyConstructorEndIL(ILGenerator il)
        {
            il.Emit(OpCodes.Ret);
        }

        private void BuildProperty(PropertyInfo sourceProperty, PropertyInfo targetProperty, TypeBuilder dtoTypeBuilder, ILGenerator dtoConstructorBuilderIL)
        {
            var backingFieldBuilder = dtoTypeBuilder.DefineField(
                    fieldName: $"<{targetProperty.Name}>k__BackingField",
                    type: targetProperty.PropertyType,
                    attributes: FieldAttributes.Private);

            if (sourceProperty.GetMethod is not null)
                EmitCopyConstructorPropertyCopyIL(dtoConstructorBuilderIL, sourceProperty.GetMethod, backingFieldBuilder);

            var propertyBuilder = dtoTypeBuilder.DefineProperty(
                name: targetProperty.Name,
                attributes: PropertyAttributes.None,
                returnType: targetProperty.PropertyType,
                parameterTypes: Type.EmptyTypes);

            // Getters and setters are always implemented on the actual DTO type
            BuildGetter(targetProperty, dtoTypeBuilder, backingFieldBuilder, propertyBuilder);
            BuildSetter(targetProperty, dtoTypeBuilder, backingFieldBuilder, propertyBuilder);
        }

        private void BuildGetter(PropertyInfo targetProperty, TypeBuilder dtoTypeBuilder,
            FieldBuilder backingFieldBuilder, PropertyBuilder propertyBuilder)
        {
            var getterBuilder = dtoTypeBuilder.DefineMethod(
                name: $"get_{propertyBuilder.Name}",
                attributes: MethodAttributes.Public | MethodAttributes.Virtual,
                returnType: targetProperty.PropertyType,
                parameterTypes: Type.EmptyTypes);

            EmitPropertyGetterIL(getterBuilder.GetILGenerator(), backingFieldBuilder);

            propertyBuilder.SetGetMethod(getterBuilder);

            // We're implementing the getter whether it exists on the DTO interface or not, so its value can be read
            // by reflection or other means, but we only need to attach it to the interface if it defines a getter
            if (targetProperty.GetMethod is not null)
                dtoTypeBuilder.DefineMethodOverride(getterBuilder, targetProperty.GetMethod);
        }

        private void EmitPropertyGetterIL(ILGenerator il, FieldInfo backingField)
        {
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldfld, backingField);
            il.Emit(OpCodes.Ret);
        }

        private void BuildSetter(PropertyInfo targetProperty, TypeBuilder dtoTypeBuilder,
            FieldBuilder backingFieldBuilder, PropertyBuilder propertyBuilder)
        {
            var setterBuilder = dtoTypeBuilder.DefineMethod(
                name: $"set_{propertyBuilder.Name}",
                attributes: MethodAttributes.Public | MethodAttributes.Virtual,
                returnType: null,
                parameterTypes: new[] { targetProperty.PropertyType });

            EmitPropertySetterIL(setterBuilder.GetILGenerator(), backingFieldBuilder);

            propertyBuilder.SetSetMethod(setterBuilder);

            // We're implementing the setter whether it exists on the DTO interface or not, so its value can be set
            // by reflection or other means, but we only need to attach it to the interface if it defines a setter
            if (targetProperty.SetMethod is not null)
                dtoTypeBuilder.DefineMethodOverride(setterBuilder, targetProperty.SetMethod);
        }

        private void EmitPropertySetterIL(ILGenerator il, FieldInfo backingField)
        {
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Stfld, backingField);
            il.Emit(OpCodes.Ret);
        }

        private void BuildPropertyStub(PropertyInfo targetProperty, TypeBuilder dtoTypeBuilder)
        {
            if (targetProperty.GetMethod is not null)
            {
                var getterBuilder = dtoTypeBuilder.DefineMethod(
                    name: $"get_{targetProperty.Name}",
                    attributes: MethodAttributes.Public | MethodAttributes.Virtual,
                    returnType: targetProperty.PropertyType,
                    parameterTypes: Type.EmptyTypes);

                EmitPropertyStubIL(getterBuilder.GetILGenerator(), targetProperty.Name);

                dtoTypeBuilder.DefineMethodOverride(getterBuilder, targetProperty.GetMethod);
            }

            if (targetProperty.SetMethod is not null)
            {
                var setterBuilder = dtoTypeBuilder.DefineMethod(
                    name: $"set_{targetProperty.Name}",
                    attributes: MethodAttributes.Public | MethodAttributes.Virtual,
                    returnType: null,
                    parameterTypes: new[] { targetProperty.PropertyType });

                EmitPropertyStubIL(setterBuilder.GetILGenerator(), targetProperty.Name);

                dtoTypeBuilder.DefineMethodOverride(setterBuilder, targetProperty.SetMethod);
            }
        }

        private void EmitPropertyStubIL(ILGenerator il, string propertyName)
        {
            il.Emit(OpCodes.Ldstr, $"This DTO type does not implement {propertyName}!");
            il.Emit(OpCodes.Newobj, typeof(NotImplementedException).GetConstructor(new[] { typeof(string) })!);
            il.Emit(OpCodes.Throw);
        }

        private void EnsureCanAccess<T>(string typeParamName)
        {
            if (!typeof(T).Attributes.HasFlag(TypeAttributes.Public) &&
                !typeof(T).Assembly.GetCustomAttributes<InternalsVisibleToAttribute>().Any(attr => attr.AssemblyName == DtoAssemblyName))
            {
                var message = $"Cannot create a DTO type for non-public type {typeof(T).Name} unless its defining assembly " +
                    $"defines a {typeof(InternalsVisibleToAttribute).Namespace}.{typeof(InternalsVisibleToAttribute).Name} attribute " +
                    $"with the DTO assembly's name (which can be passed to the {nameof(DtoTypeGenerator)} in a constructor overload).";
                throw new ArgumentException(message, typeParamName);
            }
        }
    }
}
