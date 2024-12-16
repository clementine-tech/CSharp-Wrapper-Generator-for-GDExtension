using System;
using System.Collections.Generic;
using System.Text;

namespace GDExtensionAPIGenerator;

internal static partial class CodeGenerator
{
    private static void ConstructProperties(
        HashSet<string> occupiedNames,
        IReadOnlyList<Property> propertyInfos,
        IReadOnlyDictionary<string, string> godotSharpTypeNameMap,
        IReadOnlyDictionary<string, ClassInfo> gdeTypeMap,
        StringBuilder builder
    )
    {
        if (propertyInfos.Count == 0)
        {
            return;
        }

        builder.AppendLine(
            """
            #region Properties

            """
        );

        foreach (var propertyInfo in propertyInfos)
        {
            if (propertyInfo.IsGroupOrSubgroup) continue;

            var propertyName = propertyInfo.GetPropertyName();

            if (!occupiedNames.Add(propertyName)) propertyName += "Property";

            var type = propertyInfo.GetGdType();
            var typeName = type.CSharpTypeName();
            var defaultGetter = DefaultGetter();
            switch (type)
            {
                case GdType.GdVariant:
                case GdType.Void:
                {
                    if (type is GdType.Void)
                    {
                        Godot.GD.PushWarning($"Found `void` type in property info, should be `Variant`, using Variant instead.\n" +
                                             $"Property: {propertyInfo}");
                    }

                    builder
                        .AppendLine($"{TAB1}public Variant {propertyName}")
                        .AppendLine($"{TAB1}{{")
                        .AppendLine(
                            $$"""{{TAB2}}get => Get("{{propertyInfo.NativeName}}") is { VariantType: not Variant.Type.Nil } _result ? _result : default;""")
                        .AppendLine(
                            $"""{TAB2}set => Set("{propertyInfo.NativeName}", value);""")
                        .AppendLine($"{TAB1}}}")
                        .AppendLine();
                    break;
                }
                case GdType.GdEnum:
                case GdType.EnumConstants:
                    AppendNonVariantProperty($"{defaultGetter}.As<Int64>()");
                    break;
                case GdType.TypedArray(var itemType):
                {
                    var getter = gdeTypeMap.ContainsKey(itemType.CSharpTypeName())
                        ? $"{STATIC_HELPER}.{MethodCast}<{itemType}>({defaultGetter})"
                        : defaultGetter;
                    AppendNonVariantProperty(getter);
                    break;
                }
                case GdType.BuiltIn:
                case GdType.GdObject:
                case GdType.VariantArray:
                    AppendNonVariantProperty();
                    break;
                default:
                    throw new Exception($"Unhandled type kind: {type}");
            }

            continue;

            void AppendNonVariantProperty(string getter = null)
            {
                getter ??= defaultGetter;

                builder
                    .AppendLine($"{TAB1}public {typeName} {propertyName}")
                    .AppendLine($"{TAB1}{{")
                    .AppendLine($"""{TAB2}get => {getter};""")
                    .AppendLine($"""{TAB2}set => Set("{propertyInfo.NativeName}", Variant.From(value));""")
                    .AppendLine($"{TAB1}}}")
                    .AppendLine();
            }

            string DefaultGetter() => $"({typeName}) Get(\"{propertyInfo.NativeName}\")";
        }

        builder.AppendLine(
            """
            #endregion

            """
        );
    }
}