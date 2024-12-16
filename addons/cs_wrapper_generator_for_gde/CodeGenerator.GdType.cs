using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace GDExtensionAPIGenerator;

internal static partial class CodeGenerator
{
    public abstract record GdType
    {
        // Only valid for return types of methods, when the method doesn't return anything.
        public record Void : GdType;

        public record GdVariant : GdType;

        public record VariantArray : GdType;

        public record GdObject(string ClassName) : GdType;

        public record GdEnum(string Name) : GdType;

        public record EnumConstants(string Prefix, List<(string name, long? value)> Constants) : GdType;

        public record TypedArray(GdType ItemType) : GdType;

        public record BuiltIn(string Name) : GdType;

        public string CSharpTypeName() => this switch
        {
            BuiltIn(var name) => name,
            GdEnum(var name) => name,
            EnumConstants(var prefix, _) => prefix,
            GdObject(var className) => className,
            TypedArray(var elementName) => $"Godot.Collections.Array<{elementName.CSharpTypeName()}>",
            GdVariant => "Godot.Variant",
            VariantArray => "Godot.Collections.Array",
            Void => "void",
            _ => throw new Exception($"Unhandled type kind: {this}")
        };

        public static GdType Build(Godot.Variant.Type type, string className, PropertyHint hint, string hintString, PropertyUsageFlags usage)
        {
            switch (type)
            {
                case Variant.Type.Nil:
                    return usage.HasFlag(PropertyUsageFlags.NilIsVariant) ? new GdVariant() : new Void();
                case Variant.Type.Int when hint is PropertyHint.Enum:
                    if (usage.HasFlag(PropertyUsageFlags.ClassIsEnum))
                    {
                        string enumName = string.IsNullOrWhiteSpace(className) ? "UNDEFINED_ENUM" : className;
                        return new GdEnum(enumName);
                    }
                    else if (!string.IsNullOrWhiteSpace(hintString))
                    {
                        var candidates = hintString.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                        List<(string name, long? value)> constants = candidates.Select(str =>
                        {
                            var split = str.Split(':', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                            if (split.Length == 1)
                            {
                                return (split[0], null);
                            }
                            else if (long.TryParse(split[1], out long value))
                            {
                                return (split[0], value as long?);
                            }
                            else
                            {
                                GD.PushWarning($"Failed to parse enum constant value \"{str}\".\n" +
                                               $"Split: {split}");
                                return (split[0], null);
                            }
                        }).ToList();

                        if (constants.Count is 0)
                        {
                            GD.PushWarning($"Failed to parse enum constants, using `Int64` instead. HintString: {hintString}");
                            return new BuiltIn("Int64");
                        }

                        // See if we can turn "COUNT_SUCCESSFUL,COUNT_FAILED,COUNT_ALL"
                        // into
                        // enum Count {
                        //     SUCCESSFUL,
                        //     FAILED,
                        //     ALL
                        // }
                        var underscoreIndex = constants[0].name.IndexOf('_');
                        if (underscoreIndex < 0 || underscoreIndex == constants[0].name.Length - 1)
                        {
                            return new BuiltIn("Int64");
                        }

                        var prefix = constants[0].name[..underscoreIndex];

                        for (int i = 1; i < constants.Count; i++)
                        {
                            var constant = constants[i];
                            if (!constant.name.StartsWith(prefix) || constant.name.Length <= prefix.Length)
                            {
                                return new BuiltIn("Int64");
                            }
                        }

                        var unprefixedConstants = constants
                            .Select(x => (x.name[(underscoreIndex + 1)..], x.value))
                            .ToList();

                        return new EnumConstants(EscapeAndFormatName(prefix), unprefixedConstants);
                    }
                    else
                    {
                        var kind = new BuiltIn("Int64");
                        GD.PushWarning(
                            "`PropertyHint` is `Enum` but `Usage` doesn't have flag `ClassIsEnum` and `HintString` is empty, using `long` instead.\n" +
                            $"\tType: {type}\n" +
                            $"\tUsage: {usage}\n" +
                            $"\tHint: {hint}\n" +
                            $"\tClassName: {className}\n" +
                            $"\tHintString: {hintString}\n" +
                            "This should not happen, please report this issue.");
                        return kind;
                    }
                case Variant.Type.Object:
                {
                    if (!string.IsNullOrWhiteSpace(className))
                        return new GdObject(className);
                    else break;
                }
                case Variant.Type.Array:
                {
                    if (hint is PropertyHint.ArrayType)
                    {
                        if (!string.IsNullOrWhiteSpace(className))
                            return new TypedArray(new BuiltIn(className));
                        else if (!string.IsNullOrWhiteSpace(hintString) && TryParseType(hintString, out var itemType))
                            return new TypedArray(itemType);
                        else
                        {
                            GD.PushWarning(
                                $"`PropertyHint` is `ArrayType` but `{nameof(className)}` is empty," +
                                "using `VariantArray` instead.\n" +
                                $"Type: {type}\n" +
                                $"HintString: {hintString}\n" +
                                $"Usage: {usage}\n" +
                                "This should not happen, please report this issue.");
                            return new VariantArray();
                        }
                    }
                    else
                    {
                        return new VariantArray();
                    }
                }
            }

            string rawTypeName = GetCSharpRawTypeName(type);
            if (!string.IsNullOrWhiteSpace(rawTypeName))
                return new BuiltIn(rawTypeName);
            else
                return new GdVariant();
        }

        static bool TryParseType(string hintString, out GdType type)
        {
            while (hintString.EndsWith(':'))
                hintString = hintString[..^1];

            int colonIndex = hintString.IndexOf(':');

            string beforeColon;
            string afterColon;

            if (colonIndex >= 0)
            {
                beforeColon = hintString[..colonIndex];
                afterColon = hintString[(colonIndex + 1)..];
            }
            else
            {
                beforeColon = hintString;
                afterColon = string.Empty;
            }

            var beforeSplit = beforeColon.Split('/', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            // Try parsing {Type}/{PropertyHint}:{HintString}
            if (beforeSplit.Length == 2
                && long.TryParse(beforeSplit[0], out long hintType)
                && Enum.IsDefined(typeof(Variant.Type), hintType)
                && long.TryParse(beforeSplit[1], out long propertyHint)
                && Enum.IsDefined(typeof(PropertyHint), propertyHint))
            {
                type = Build((Variant.Type)hintType, "", (PropertyHint)propertyHint, afterColon, PropertyUsageFlags.None);
                return true;
            }
            // Try parsing {Type}:{HintString}
            else if (long.TryParse(beforeColon, out hintType)
                     && Enum.IsDefined(typeof(Variant.Type), hintType))
            {
                type = Build((Variant.Type)hintType, "", PropertyHint.None, afterColon, PropertyUsageFlags.None);
                return true;
            }

            if (hintString.IsValidIdentifier())
            {
                type = new BuiltIn(beforeColon);
                return true;
            }

            type = null;
            return false;
        }

        static string GetCSharpRawTypeName(Variant.Type type) =>
            type switch
            {
                Variant.Type.Aabb => "Aabb",
                Variant.Type.Basis => "Basis",
                Variant.Type.Callable => "Callable",
                Variant.Type.Color => "Color",
                Variant.Type.NodePath => "NodePath",
                Variant.Type.Plane => "Plane",
                Variant.Type.Projection => "Projection",
                Variant.Type.Quaternion => "Quaternion",
                Variant.Type.Rect2 => "Rect2",
                Variant.Type.Rect2I => "Rect2I",
                Variant.Type.Rid => "Rid",
                Variant.Type.Signal => "Signal",
                Variant.Type.Nil => "Godot.Variant",
                Variant.Type.StringName => "StringName",
                Variant.Type.Transform2D => "Transform2D",
                Variant.Type.Transform3D => "Transform3D",
                Variant.Type.Vector2 => "Vector2",
                Variant.Type.Vector2I => "Vector2I",
                Variant.Type.Vector3 => "Vector3",
                Variant.Type.Vector3I => "Vector3I",
                Variant.Type.Vector4 => "Vector4",
                Variant.Type.Vector4I => "Vector4I",
                Variant.Type.PackedByteArray => "byte[]",
                Variant.Type.PackedInt32Array => "Int32[]",
                Variant.Type.PackedInt64Array => "Int64[]",
                Variant.Type.PackedFloat32Array => "float[]",
                Variant.Type.PackedFloat64Array => "double[]",
                Variant.Type.PackedStringArray => "string[]",
                Variant.Type.PackedVector2Array => "Vector2[]",
                Variant.Type.PackedVector3Array => "Vector3[]",
                Variant.Type.PackedColorArray => "Color[]",
                Variant.Type.Bool => "bool",
                Variant.Type.Int => "Int64",
                Variant.Type.Float => "float",
                Variant.Type.String => "string",
                Variant.Type.Dictionary => "Godot.Collections.Dictionary",
                Variant.Type.Array => "Godot.Collections.Array",
                Variant.Type.Object => "Object",
                _ => string.Empty
            };
    }
}