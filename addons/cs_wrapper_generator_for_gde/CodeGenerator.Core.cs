using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Godot;

namespace GDExtensionAPIGenerator;

internal static partial class CodeGenerator
{
    private static (string typeName, string code, List<CodeGenerator.GdType.EnumConstants> enums) GenerateSourceCodeForType(
        ClassInfo gdeTypeInfo,
        IReadOnlyDictionary<string, string> godotSharpTypeNameMap,
        IReadOnlyDictionary<string, ClassInfo> gdeTypeMap,
        ICollection<string> godotBuiltinClassNames,
        ConcurrentDictionary<string, string> enumNameToConstantMap)
    {
        var (code, enums) = GenerateCode(
            gdeTypeInfo,
            gdeTypeMap,
            godotSharpTypeNameMap,
            godotBuiltinClassNames,
            enumNameToConstantMap
        );

        return (gdeTypeInfo.TypeName, code, enums);
    }

    private const string TAB1 = "    ";
    private const string TAB2 = TAB1 + TAB1;
    private const string TAB3 = TAB2 + TAB1;
    private const string TAB4 = TAB2 + TAB2;
    private const string TAB5 = TAB4 + TAB1;
    private const string TAB6 = TAB3 + TAB3;
    private const string NAMESPACE = "GDExtension.Wrappers";
    private const string MethodCreateInstance = "Instantiate";
    private const string GDExtensionName = "GDExtensionName";
    private const string MethodBind = "Bind";
    private const string MethodCast = "Cast";

    private static (string code, List<CodeGenerator.GdType.EnumConstants> enums) GenerateCode(
        ClassInfo gdeTypeInfo,
        IReadOnlyDictionary<string, ClassInfo> gdeTypeMap,
        IReadOnlyDictionary<string, string> godotSharpTypeNameMap,
        ICollection<string> godotBuiltinClassNames,
        ConcurrentDictionary<string, string> enumNameToConstantMap)
    {
        var codeBuilder = new StringBuilder();
        var displayTypeName = godotSharpTypeNameMap.GetValueOrDefault(gdeTypeInfo.TypeName, gdeTypeInfo.TypeName);
        var displayParentTypeName = godotSharpTypeNameMap.GetValueOrDefault(gdeTypeInfo.ParentType.TypeName, gdeTypeInfo.ParentType.TypeName);

        var isAbstract = !ClassDB.CanInstantiate(gdeTypeInfo.TypeName);

        var engineBaseType = GetEngineBaseType(gdeTypeInfo, godotBuiltinClassNames);

        var isRootWrapper = gdeTypeInfo.ParentType.TypeName == engineBaseType || godotBuiltinClassNames.Contains(gdeTypeInfo.ParentType.TypeName);

        engineBaseType = godotSharpTypeNameMap.GetValueOrDefault(engineBaseType, engineBaseType);

        var abstractKeyWord = isAbstract ? "abstract " : string.Empty;
        var newKeyWord = isRootWrapper ? string.Empty : "new ";

        codeBuilder.AppendLine(
            $$"""
              using System;
              using Godot;
              using Object = Godot.GodotObject;

              namespace {{NAMESPACE}};

              public {{abstractKeyWord}}partial class {{displayTypeName}} : {{displayParentTypeName}}
              {
              {{TAB1}}public {{newKeyWord}}static readonly StringName {{GDExtensionName}} = "{{gdeTypeInfo.TypeName}}";

              {{TAB1}}/// <summary>
              {{TAB1}}/// Creates an instance of the GDExtension <see cref="{{displayTypeName}}"/> type, and attaches the wrapper script to it.
              {{TAB1}}/// </summary>
              {{TAB1}}/// <returns>The wrapper instance linked to the underlying GDExtension type.</returns>
              {{TAB1}}public {{newKeyWord}}static {{displayTypeName}} {{MethodCreateInstance}}()
              {{TAB1}}{
              {{TAB2}}return {{STATIC_HELPER}}.{{MethodCreateInstance}}<{{displayTypeName}}>({{GDExtensionName}});
              {{TAB1}}}

              {{TAB1}}/// <summary>
              {{TAB1}}/// Try to cast the script on the supplied <paramref name="godotObject"/> to the <see cref="{{displayTypeName}}"/> wrapper type,
              {{TAB1}}/// if no script has attached to the type, or the script attached to the type does not inherit the <see cref="{{displayTypeName}}"/> wrapper type,
              {{TAB1}}/// a new instance of the <see cref="{{displayTypeName}}"/> wrapper script will get attaches to the <paramref name="godotObject"/>.
              {{TAB1}}/// </summary>
              {{TAB1}}/// <remarks>The developer should only supply the <paramref name="godotObject"/> that represents the correct underlying GDExtension type.</remarks>
              {{TAB1}}/// <param name="godotObject">The <paramref name="godotObject"/> that represents the correct underlying GDExtension type.</param>
              {{TAB1}}/// <returns>The existing or a new instance of the <see cref="{{displayTypeName}}"/> wrapper script attached to the supplied <paramref name="godotObject"/>.</returns>
              {{TAB1}}public {{newKeyWord}}static {{displayTypeName}} {{MethodBind}}(GodotObject godotObject)
              {{TAB1}}{
              {{TAB2}}return {{STATIC_HELPER}}.{{MethodBind}}<{{displayTypeName}}>(godotObject);
              {{TAB1}}}
              """
        );

        var enums = GenerateMembers(
            codeBuilder,
            gdeTypeInfo,
            gdeTypeMap,
            godotSharpTypeNameMap,
            godotBuiltinClassNames,
            enumNameToConstantMap
        );

        var code = codeBuilder.Append('}').ToString();
        return (code, enums);
    }


    private static List<CodeGenerator.GdType.EnumConstants> GenerateMembers(
        StringBuilder codeBuilder,
        ClassInfo gdeTypeInfo,
        IReadOnlyDictionary<string, ClassInfo> gdeTypeMap,
        IReadOnlyDictionary<string, string> godotSharpTypeNameMap,
        ICollection<string> godotBuiltinClassNames,
        ConcurrentDictionary<string, string> enumConstantMap
    )
    {
        var propertyInfoList = CollectPropertyInfo(gdeTypeInfo);
        var methodInfoList = CollectMethodInfo(gdeTypeInfo, propertyInfoList);
        var signalInfoList = CollectSignalInfo(gdeTypeInfo);
        var enumInfoList = CollectEnumInfo(gdeTypeInfo);
        var occupiedNames = new HashSet<string>();
        var enumsBuilder = new StringBuilder();
        var signalsBuilder = new StringBuilder();
        var propertiesBuilder = new StringBuilder();
        var methodsBuilder = new StringBuilder();

        ConstructProperties(occupiedNames, propertyInfoList, godotSharpTypeNameMap, gdeTypeMap, propertiesBuilder);
        ConstructMethods(occupiedNames, methodInfoList, godotSharpTypeNameMap, gdeTypeMap, godotBuiltinClassNames, methodsBuilder, gdeTypeInfo);
        ConstructSignals(occupiedNames, signalInfoList, signalsBuilder, gdeTypeMap, godotSharpTypeNameMap);
        ConstructEnums(occupiedNames, enumInfoList, enumsBuilder, gdeTypeInfo, enumConstantMap);

        codeBuilder
            .Append(enumsBuilder)
            .Append(propertiesBuilder)
            .Append(signalsBuilder)
            .Append(methodsBuilder);

        var enums = new List<CodeGenerator.GdType.EnumConstants>();
        foreach (var prop in propertyInfoList)
            if (prop.GetGdType() is CodeGenerator.GdType.EnumConstants constants)
                enums.Add(constants);

        foreach (var method in methodInfoList)
        {
            if (method.ReturnValue.GetGdType() is CodeGenerator.GdType.EnumConstants constants) enums.Add(constants);

            foreach (var argument in method.Arguments)
                if (argument.GetGdType() is CodeGenerator.GdType.EnumConstants argConstants)
                    enums.Add(argConstants);
        }

        foreach (var signal in signalInfoList)
        {
            if (signal.ReturnValue.GetGdType() is CodeGenerator.GdType.EnumConstants constants) enums.Add(constants);

            foreach (var argument in signal.Arguments)
                if (argument.GetGdType() is CodeGenerator.GdType.EnumConstants argConstants)
                    enums.Add(argConstants);
        }

        return enums;
    }

    private const string UNRESOLVED_ENUM_HINT = "ENUM_HINT";
    private const string UNRESOLVED_ENUM_TEMPLATE = $"<UNRESOLVED_ENUM_TYPE>{UNRESOLVED_ENUM_HINT}</UNRESOLVED_ENUM_TYPE>";

    [GeneratedRegex(@"<UNRESOLVED_ENUM_TYPE>(?<EnumConstants>.*)<\/UNRESOLVED_ENUM_TYPE>")]
    private static partial Regex GetExtractUnResolvedEnumValueRegex();

    private static string GetEngineBaseType(ClassInfo gdeTypeInfo, ICollection<string> builtinTypes)
    {
        while (true)
        {
            if (builtinTypes.Contains(gdeTypeInfo.TypeName)) return gdeTypeInfo.TypeName;
            gdeTypeInfo = gdeTypeInfo.ParentType;
        }
    }

    private static void BuildupMethodArguments(StringBuilder stringBuilder, Property[] propertyInfos,
        IReadOnlyDictionary<string, string> godotsharpTypeNameMap)
    {
        for (var i = 0; i < propertyInfos.Length; i++)
        {
            if (i > 0) stringBuilder.Append(", ");

            var propertyInfo = propertyInfos[i];
            var typeKind = propertyInfo.GetGdType();

            string argumentTypeName = typeKind switch
            {
                CodeGenerator.GdType.BuiltIn or CodeGenerator.GdType.GdEnum or CodeGenerator.GdType.EnumConstants or CodeGenerator.GdType.GdObject or
                    CodeGenerator.GdType.GdVariant or CodeGenerator.GdType.TypedArray or CodeGenerator.GdType.VariantArray
                    => typeKind.CSharpTypeName(),
                CodeGenerator.GdType.Void => throw new Exception($"Unexpected `void` type in method argument info.\nPropertyInfo: {propertyInfo}"),
                _ => throw new Exception($"Unhandled type kind: {typeKind}")
            };

            stringBuilder.Append($"{argumentTypeName} {propertyInfo.GetArgumentName()}");
        }
    }

    [GeneratedRegex(@"[^a-zA-Z0-9_]")]
    private static partial Regex EscapeNameRegex();

    [GeneratedRegex(@"[0-9]+")]
    private static partial Regex EscapeNameDigitRegex();


    // TODO: Split escape types, some of these keywords are actually valid method argument name.
    private static readonly HashSet<string> _csKeyword =
    [
        "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked", "class", "const",
        "continue", "decimal", "default", "delegate", "do", "double", "else", "enum", "event", "explicit", "extern",
        "false", "finally", "fixed", "float", "for", "foreach", "goto", "if", "implicit", "in", "int", "interface",
        "internal", "is", "lock", "long", "namespace", "new", "null", "object", "operator", "out", "override", "params",
        "private", "protected", "public", "readonly", "ref", "return", "sbyte", "sealed", "short", "sizeof", "stackalloc",
        "static", "string", "struct", "switch", "this", "throw", "true", "try", "typeof", "uint", "ulong", "unchecked",
        "unsafe", "ushort", "using", "virtual", "void", "volatile", "while",
        "string", "object", "var", "dynamic", "yield", "add", "alias", "ascending", "async", "await", "by",
        "descending", "equals", "from", "get", "global", "group", "into", "join", "let", "nameof", "on", "orderby",
        "partial", "remove", "select", "set", "when", "where", "yield"
    ];

    private static string EscapeAndFormatName(string sourceName, bool camelCase = false)
    {
        ArgumentOutOfRangeException.ThrowIfNullOrEmpty(sourceName);

        var name = EscapeNameRegex()
            .Replace(sourceName, "_")
            .ToPascalCase();

        if (camelCase) name = ToCamelCase(name);

        if (_csKeyword.Contains(name)) name = $"@{name}";

        if (EscapeNameDigitRegex().IsMatch(name[..1])) name = $"_{name}";

        return name;
    }

    public static string ToCamelCase(string sourceName)
    {
        if (string.IsNullOrWhiteSpace(sourceName)) return sourceName;
        return sourceName[..1].ToLowerInvariant() + sourceName[1..];
    }
}