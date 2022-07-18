using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace MrHotkeys.Linq.LateBinding.Dto
{
    internal static class EmitHelpers
    {
        public enum Accessibility
        {
            Public,
            Private,
            Protected,
            Internal,
            ProtectedInternal,
            PrivateProtected,
        }

        public static MethodAttributes ToMethodAttributes(this Accessibility a) => a switch
        {
            Accessibility.Public => MethodAttributes.Public,
            Accessibility.Private => MethodAttributes.Private,
            Accessibility.Protected => MethodAttributes.Family,
            Accessibility.Internal => MethodAttributes.Assembly,
            Accessibility.ProtectedInternal => MethodAttributes.FamORAssem,
            Accessibility.PrivateProtected => MethodAttributes.FamANDAssem,
            _ => throw new InvalidOperationException(),
        };

        public static MethodBuilder DefineMethodAndOverride(this TypeBuilder typeBuilder, string name,
            int genericParameterCount, BindingFlags bindingAttr, params Type[] parameterTypes)
        {
            if (typeBuilder is null)
                throw new ArgumentNullException(nameof(typeBuilder));
            if (name is null)
                throw new ArgumentNullException(nameof(name));
            if (parameterTypes is null)
                throw new ArgumentNullException(nameof(parameterTypes));

            var baseMethod = typeBuilder
                .BaseType
                .GetMethod(name, genericParameterCount, bindingAttr, parameterTypes);

            if (baseMethod.IsFinal)
                throw new InvalidOperationException($"Cannot override sealed method {name}!");
            if (!baseMethod.IsVirtual)
                throw new InvalidOperationException($"Cannot override non-virtual method {name}!");

            var overMethodBuilder = typeBuilder.DefineMethod(
                name: baseMethod.Name,
                attributes: baseMethod.Attributes & ~MethodAttributes.Abstract,
                callingConvention: baseMethod.CallingConvention,
                returnType: baseMethod.ReturnType,
                parameterTypes: baseMethod
                    .GetParameters()
                    .Select(p => p.ParameterType)
                    .ToArray()
            );

            typeBuilder.DefineMethodOverride(overMethodBuilder, baseMethod);

            return overMethodBuilder;
        }

        public static PropertyBuilder DefineAutoProperty(TypeBuilder typeBuilder, string name,
            Type type, Accessibility setterAccess, Accessibility getterAccess)
        {
            if (typeBuilder is null)
                throw new ArgumentNullException(nameof(typeBuilder));
            if (name is null)
                throw new ArgumentNullException(nameof(name));
            if (type is null)
                throw new ArgumentNullException(nameof(type));

            var backingFieldBuilder = DefineAutoPropertyBackingField(typeBuilder, name, type);

            var propertyBuilder = typeBuilder.DefineProperty(
                name: name,
                attributes: PropertyAttributes.None,
                returnType: type,
                parameterTypes: Type.EmptyTypes
            );

            var getterBuilder = DefineAutoPropertyGetter(typeBuilder, name, type, setterAccess, backingFieldBuilder);
            propertyBuilder.SetGetMethod(getterBuilder);

            var setterBuilder = DefineAutoPropertySetter(typeBuilder, name, type, getterAccess, backingFieldBuilder);
            propertyBuilder.SetSetMethod(setterBuilder);

            return propertyBuilder;
        }

        public static FieldBuilder DefineAutoPropertyBackingField(TypeBuilder typeBuilder, string propertyName, Type type)
        {
            if (typeBuilder is null)
                throw new ArgumentNullException(nameof(typeBuilder));
            if (propertyName is null)
                throw new ArgumentNullException(nameof(propertyName));
            if (type is null)
                throw new ArgumentNullException(nameof(type));

            return typeBuilder.DefineField(
                fieldName: $"<{propertyName}>k__BackingField",
                type: type,
                attributes: FieldAttributes.Private
            );
        }

        public static MethodBuilder DefineAutoPropertyGetter(TypeBuilder typeBuilder, string propertyName,
            Type type, Accessibility access, FieldBuilder backingFieldBuilder)
        {
            if (typeBuilder is null)
                throw new ArgumentNullException(nameof(typeBuilder));
            if (type is null)
                throw new ArgumentNullException(nameof(type));
            if (backingFieldBuilder is null)
                throw new ArgumentNullException(nameof(backingFieldBuilder));

            var getterBuilder = typeBuilder.DefineMethod(
                name: $"get_{propertyName}",
                attributes: access.ToMethodAttributes() | MethodAttributes.Virtual,
                returnType: type,
                parameterTypes: Type.EmptyTypes);

            var il = getterBuilder.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldfld, backingFieldBuilder);
            il.Emit(OpCodes.Ret);

            return getterBuilder;
        }

        public static MethodBuilder DefineAutoPropertySetter(TypeBuilder typeBuilder, string propertyName,
            Type type, Accessibility access, FieldBuilder backingFieldBuilder)
        {
            if (typeBuilder is null)
                throw new ArgumentNullException(nameof(typeBuilder));
            if (type is null)
                throw new ArgumentNullException(nameof(type));
            if (backingFieldBuilder is null)
                throw new ArgumentNullException(nameof(backingFieldBuilder));

            var setterBuilder = typeBuilder.DefineMethod(
                name: $"set_{propertyName}",
                attributes: access.ToMethodAttributes() | MethodAttributes.Virtual,
                returnType: null,
                parameterTypes: new[] { type });

            var il = setterBuilder.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Stfld, backingFieldBuilder);
            il.Emit(OpCodes.Ret);

            return setterBuilder;
        }

        /// <summary>
        /// Pops a string off the stack, and uses it as the comparison value for a string jump table.
        /// </summary>
        /// <param name="il">The IL generator to use.</param>
        /// <param name="caseValues">0 or more test values to use for the cases.</param>
        /// <param name="emitCaseCallback">A callback to emit code for each test case. The value for the test case
        ///     is given as the second parameter. Should return true if a break should be emitted by this method, or
        ///     false if not necessary (e.g. if a return was emitted).</param>
        /// <param name="emitDefaultCallback">A callback to emit code for the default case. Should return true if a break
        ///     should be emitted by this method, or false if not necessary (e.g. if a return was emitted).</param>
        /// <exception cref="ArgumentNullException"><paramref name="il"/>, <paramref name="caseValues"/>,
        ///     <paramref name="emitCaseCallback"/>, or <paramref name="emitDefaultCallback"/> is null.</exception>
        public static void EmitStringJumpTable(ILGenerator il, IEnumerable<string> caseValues,
            Func<ILGenerator, string, bool> emitCaseCallback, Func<ILGenerator, bool> emitDefaultCallback)
        {
            if (il is null)
                throw new ArgumentNullException(nameof(il));
            if (caseValues is null)
                throw new ArgumentNullException(nameof(caseValues));
            if (emitCaseCallback is null)
                throw new ArgumentNullException(nameof(emitCaseCallback));
            if (emitDefaultCallback is null)
                throw new ArgumentNullException(nameof(emitDefaultCallback));

            var jumpTable = new List<(Label, string)>();

            var switchValueLocal = il.DeclareLocal(typeof(string));
            il.Emit(OpCodes.Stloc, switchValueLocal);

            var stringEqualsMethod = typeof(string)
                .GetMethod(
                    name: nameof(string.Equals),
                    genericParameterCount: 0,
                    bindingAttr: BindingFlags.Public | BindingFlags.Static,
                    types: new[] { typeof(string), typeof(string) }
                ) ?? throw new InvalidOperationException();
            foreach (var caseValue in caseValues)
            {
                var caseLabel = il.DefineLabel();
                jumpTable.Add((caseLabel, caseValue));

                il.Emit(OpCodes.Ldloc, switchValueLocal);
                il.Emit(OpCodes.Ldstr, caseValue);
                il.Emit(OpCodes.Call, stringEqualsMethod);
                il.Emit(OpCodes.Brtrue, caseLabel);
            }

            var defaultLabel = il.DefineLabel();
            il.Emit(OpCodes.Br, defaultLabel);

            var breakLabel = il.DefineLabel();

            foreach (var (caseLabel, caseValue) in jumpTable)
            {
                il.MarkLabel(caseLabel);

                if (emitCaseCallback(il, caseValue))
                    il.Emit(OpCodes.Br, breakLabel);
            }

            il.MarkLabel(defaultLabel);
            if (emitDefaultCallback(il))
                il.Emit(OpCodes.Br, breakLabel);

            il.MarkLabel(breakLabel);
        }
    }
}