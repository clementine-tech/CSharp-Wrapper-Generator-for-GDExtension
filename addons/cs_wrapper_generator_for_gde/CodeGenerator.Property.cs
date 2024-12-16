using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Godot.Collections;

namespace GDExtensionAPIGenerator;

internal static partial class CodeGenerator
{
    private readonly struct Property
    {
        public readonly Variant.Type Type = Variant.Type.Nil;
        public readonly string NativeName;
        public readonly string ClassName;
        public readonly PropertyHint Hint = PropertyHint.None;
        public readonly string HintString;
        public readonly PropertyUsageFlags Usage = PropertyUsageFlags.Default;
        public readonly string TypeClass;

        public Property(Dictionary dictionary)
        {
            using var nameInfo = dictionary["name"];
            using var classNameInfo = dictionary["class_name"];
            using var typeInfo = dictionary["type"];
            using var hintInfo = dictionary["hint"];
            using var hintStringInfo = dictionary["hint_string"];
            using var usageInfo = dictionary["usage"];

            Type = typeInfo.As<Variant.Type>();
            NativeName = nameInfo.AsString();
            ClassName = classNameInfo.AsString();
            Hint = hintInfo.As<PropertyHint>();
            HintString = hintStringInfo.AsString();
            Usage = usageInfo.As<PropertyUsageFlags>();
            if (Hint is PropertyHint.Enum && Type is Variant.Type.Int)
            {
                var enumCandidates = HintString.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                TypeClass = UNRESOLVED_ENUM_TEMPLATE.Replace(UNRESOLVED_ENUM_HINT, string.Join(',', enumCandidates));
            }
            else
            {
                TypeClass = ClassName;
                if (string.IsNullOrEmpty(TypeClass))
                {
                    TypeClass = Type is Variant.Type.Array && Hint is PropertyHint.ArrayType && HintString.Contains
                        (':')
                        ? HintString[(HintString.IndexOf(':') + 1)..]
                        : HintString;
                }
                
                if (string.IsNullOrEmpty(TypeClass)) TypeClass = nameof(Variant);
            }
        }

        public bool IsGroupOrSubgroup => Usage.HasFlag(PropertyUsageFlags.Group) || Usage.HasFlag(PropertyUsageFlags.Subgroup);

        public GdType GetGdType() => GdType.Build(Type, ClassName, Hint, HintString, Usage);

        public string GetPropertyName() => EscapeAndFormatName(NativeName);

        public string GetArgumentName() => EscapeAndFormatName(NativeName, true);

#if GODOT4_4_OR_GREATER
        public bool IsProperty(string methodName) => methodName == PropertyGetter || methodName == PropertySetter;
        public string PropertyGetter => ClassDB.ClassGetPropertyGetter(ClassName, NativeName);
        public string PropertySetter => ClassDB.ClassGetPropertySetter(ClassName, NativeName);
        public override string ToString() =>
            $"""
             PropertyInfo:
             {TAB1}{nameof(Type)}: {Type}
             {TAB1}{nameof(NativeName)}: {NativeName}
             {TAB1}{nameof(ClassName)}: {ClassName}
             {TAB1}{nameof(Hint)}: {Hint}
             {TAB1}{nameof(HintString)}: {HintString}
             {TAB1}{nameof(Usage)}: {Usage}
             {TAB1}{nameof(IsGroupOrSubgroup)}: {IsGroupOrSubgroup}
             {TAB1}{nameof(IsVoid)}: {IsVoid}
             {TAB1}{nameof(TypeClass)}: {TypeClass}
             {TAB1}{nameof(PropertyGetter)}: {PropertyGetter}
             {TAB1}{nameof(PropertySetter)}: {PropertySetter}
             """;

#else
        public override string ToString() => FormatPropertyInfo(this, GetGdType());
#endif

        static string FormatPropertyInfo(Property property, GdType kind)
            => $"""
                Property Kind: {kind}
                {TAB1}{nameof(Type)}: {property.Type}
                {TAB1}{nameof(NativeName)}: {property.NativeName}
                {TAB1}{nameof(ClassName)}: {property.ClassName}
                {TAB1}{nameof(Hint)}: {property.Hint}
                {TAB1}{nameof(HintString)}: {property.HintString}
                {TAB1}{nameof(Usage)}: {property.Usage}
                {TAB1}{nameof(IsGroupOrSubgroup)}: {property.IsGroupOrSubgroup}
                {TAB1}{nameof(TypeClass)}: {property.TypeClass}
                """;
    }
}