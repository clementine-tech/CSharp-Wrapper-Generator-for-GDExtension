using System;
using System.Collections.Generic;
using System.Text;
using Godot;

namespace GDExtensionAPIGenerator;

internal static partial class CodeGenerator
{
    private static void ConstructMethods(
        HashSet<string> occupiedNames,
        IReadOnlyList<MethodInfo> methodInfoList,
        IReadOnlyDictionary<string, string> godotSharpTypeNameMap,
        IReadOnlyDictionary<string, ClassInfo> gdeTypeMap,
        ICollection<string> builtinTypeNames,
        StringBuilder builder,
        ClassInfo classInfo
    )
    {
        if (methodInfoList.Count == 0)
        {
            return;
        }

        builder.AppendLine(
            """
            #region Methods

            """
        );


        foreach (var methodInfo in methodInfoList)
        {
            var methodNativeName = methodInfo.NativeName;

            var methodName = methodInfo.GetMethodName();

            if (!occupiedNames.Add(methodName)) methodName += "Method";

            builder.Append($"{TAB1}public ");

            var isVirtual = methodInfo.Flags.HasFlag(MethodFlags.Virtual);
            var isStatic = methodInfo.Flags.HasFlag(MethodFlags.Static);

            if (isStatic) builder.Append("static ");
            if (isVirtual) builder.Append("virtual ");

            var returnType = methodInfo.ReturnValue.GetGdType();

            builder.Append($"{returnType.CSharpTypeName()} {methodName}(");
            BuildupMethodArguments(builder, methodInfo.Arguments, godotSharpTypeNameMap);
            builder.AppendLine(") =>");
            builder.Append($"{TAB2}");

            // TODO: VIRTUAL

            switch (returnType)
            {
                case GdType.GdObject(var className) when gdeTypeMap.ContainsKey(className):
                {
                    builder.Append($"{STATIC_HELPER}.{MethodBind}<{className}>(");
                    AppendCallInvocation();
                    builder.Append(".As<Object>())");
                    break;
                }
                case GdType.TypedArray(var itemType) when gdeTypeMap.ContainsKey(itemType.CSharpTypeName()):
                {
                    builder.Append($"{STATIC_HELPER}.{MethodCast}<{itemType.CSharpTypeName()}>(");
                    AppendCallInvocation();
                    builder.Append(".As<Godot.Collections.Array<Object>>())");
                    break;
                }
                case GdType.GdObject:
                case GdType.TypedArray:
                case GdType.BuiltIn:
                case GdType.GdEnum:
                case GdType.EnumConstants:
                case GdType.GdVariant:
                case GdType.VariantArray:
                {
                    AppendCallInvocation();
                    builder.Append($".As<{returnType.CSharpTypeName()}>()");
                    break;
                }
                case GdType.Void:
                    AppendCallInvocation();
                    break;
                default:
                    throw new Exception($"Unhandled type kind: {returnType}");
            }

            // TODO: var isVararg = methodInfo.Flags.HasFlag(MethodFlags.Vararg);

            builder.AppendLine(";").AppendLine();
            continue;

            void AppendCallInvocation()
            {
                if (isStatic)
                    builder.Append($"{STATIC_HELPER}.Call(\"{classInfo.TypeName}\", \"{methodNativeName}\"");
                else
                    builder.Append($"Call(\"{methodNativeName}\"");

                BuildupMethodCallArguments(
                    builder,
                    methodInfo.Arguments,
                    gdeTypeMap,
                    godotSharpTypeNameMap,
                    builtinTypeNames
                );

                builder.Append(')');
            }
        }


        builder.AppendLine(
            """
            #endregion

            """
        );
    }

    private static void BuildupMethodCallArguments(
        StringBuilder builder,
        Property[] propertyInfos,
        IReadOnlyDictionary<string, ClassInfo> gdeTypeMap,
        IReadOnlyDictionary<string, string> godotSharpTypeMap,
        ICollection<string> builtinTypes
    )
    {
        foreach (Property propertyInfo in propertyInfos)
        {
            builder.Append(", ");
            var type = propertyInfo.GetGdType();
            var argumentName = propertyInfo.GetArgumentName();

            switch (type)
            {
                case GdType.GdObject(var className) when gdeTypeMap.TryGetValue(className, out var gdeClassInfo):
                {
                    var baseType = GetEngineBaseType(gdeClassInfo, builtinTypes);
                    var typeName = godotSharpTypeMap.GetValueOrDefault(baseType) ?? baseType;
                    builder.Append($"({typeName}) {argumentName}");
                    break;
                }
                case GdType.GdEnum(var name):
                    builder.Append($"Variant.From<{name}>({argumentName})");
                    break;
                case GdType.EnumConstants:
                    builder.Append($"Variant.From<Int64>({argumentName} as Int64)");
                    break;
                case GdType.GdVariant:
                    builder.Append($"{argumentName}");
                    break;
                case GdType.GdObject:
                case GdType.BuiltIn:
                case GdType.TypedArray:
                case GdType.VariantArray:
                    builder.Append(argumentName);
                    break;
                case GdType.Void:
                    throw new ArgumentException("Unexpected `void` type in method argument info.");
                default:
                    throw new Exception($"Unhandled type kind: {type}");
            }
        }
    }
}