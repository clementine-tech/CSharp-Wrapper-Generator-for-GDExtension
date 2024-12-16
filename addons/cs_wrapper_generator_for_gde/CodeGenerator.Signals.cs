using System;
using System.Collections.Generic;
using System.Text;
using Godot;

namespace GDExtensionAPIGenerator;

internal static partial class CodeGenerator
{
    private static void ConstructSignals(
        HashSet<string> occupiedNames,
        IReadOnlyList<MethodInfo> signalList,
        StringBuilder builder,
        IReadOnlyDictionary<string, ClassInfo> gdeTypeMap,
        IReadOnlyDictionary<string, string> godotSharpTypeNameMap
    )
    {
        if (signalList.Count == 0) return;

        builder.AppendLine(
            """
            #region Signals

            """
        );

        foreach (var signalInfo in signalList)
        {
            var signalName = signalInfo.GetMethodName();

            if (!occupiedNames.Add(signalName)) signalName += "Signal";

            var signalDelegateName = $"{signalName}Handler";
            var signalNameCamelCase = ToCamelCase(signalName);
            var backingDelegateName = $"_{signalNameCamelCase}_backing";
            var backingCallableName = $"_{signalNameCamelCase}_backing_callable";

            var returnType = signalInfo.ReturnValue.GetGdType();
            var returnTypeName = returnType.CSharpTypeName();

            builder.Append($"{TAB1}public delegate {returnTypeName} {signalDelegateName}(");
            BuildupMethodArguments(builder, signalInfo.Arguments, godotSharpTypeNameMap);
            builder.AppendLine(");");
            builder.AppendLine();

            const string callableName = nameof(Callable);

            builder.Append(
                $$"""
                  {{TAB1}}private {{signalDelegateName}} {{backingDelegateName}};
                  {{TAB1}}private {{callableName}} {{backingCallableName}};
                  {{TAB1}}public event {{signalDelegateName}} {{signalName}}
                  {{TAB1}}{
                  {{TAB2}}add
                  {{TAB2}}{
                  {{TAB3}}if({{backingDelegateName}} == null)
                  {{TAB3}}{
                  {{TAB4}}{{backingCallableName}} = {{callableName}}.From
                  """
            );

            var argumentsLength = signalInfo.Arguments.Length;

            if (argumentsLength <= 0)
            {
                builder.AppendLine(
                    $$"""
                      (
                      {{TAB5}}() =>
                      {{TAB5}}{
                      {{TAB6}}{{backingDelegateName}}?.Invoke();
                      """
                );
                AppendAfterArguments();
                continue;
            }

            builder.Append('<');

            builder.Append(nameof(Variant));
            for (var i = 1; i < argumentsLength; i++) builder.Append($", {nameof(Variant)}");

            builder.Append('>');

            builder.Append(
                $"""
                 (
                 {TAB5}(
                 """
            );

            const string argPrefix = "arg";
            const string variantPostfix = "_variant";

            static string UnmanagedArg(int index) => $"{argPrefix}{index}{variantPostfix}";

            static string Arg(int index) => $"{argPrefix}{index}";

            builder.Append(UnmanagedArg(0));
            for (var i = 1; i < argumentsLength; i++) builder.Append($", {UnmanagedArg(i)}");

            builder.AppendLine(
                $$"""
                  ) =>
                  {{TAB5}}{
                  """
            );

            for (var index = 0; index < signalInfo.Arguments.Length; index++)
            {
                Property argumentInfo = signalInfo.Arguments[index];
                var variantArgName = UnmanagedArg(index);
                var convertedArgName = Arg(index);
                builder.Append($"{TAB6}var {convertedArgName} = ");

                var argumentKind = argumentInfo.GetGdType();
                switch (argumentKind)
                {
                    case GdType.GdObject(var className) when gdeTypeMap.ContainsKey(className):
                        builder.AppendLine($"{STATIC_HELPER}.{MethodBind}<{className}>({variantArgName}.As<Object>());");
                        break;
                    case GdType.TypedArray(var itemType) when gdeTypeMap.ContainsKey(itemType.CSharpTypeName()):
                        builder.AppendLine(
                            $"{STATIC_HELPER}.{MethodCast}<{itemType.CSharpTypeName()}>({variantArgName}.As<Godot.Collections.Array<Object>>());"
                        );
                        break;
                    case GdType.GdObject:
                    case GdType.TypedArray:
                    case GdType.BuiltIn:
                    case GdType.GdEnum:
                    case GdType.EnumConstants:
                    case GdType.GdVariant:
                    case GdType.VariantArray:
                        builder.AppendLine($"{variantArgName}.As<{argumentKind.CSharpTypeName()}>();");
                        break;
                    case GdType.Void:
                        throw new Exception($"Unexpected `void` type in signal argument info.\nSignalInfo: {signalInfo}");
                    default:
                        throw new Exception($"Unhandled type kind: {argumentKind}");
                }
            }

            builder.Append($"{TAB6}{backingDelegateName}?.Invoke(");

            builder.Append(Arg(0));
            for (var i = 1; i < argumentsLength; i++) builder.Append($", {Arg(i)}");

            builder.AppendLine(");");
            AppendAfterArguments();
            continue;

            void AppendAfterArguments()
            {
                builder.AppendLine(
                    $$"""
                      {{TAB5}}}
                      {{TAB4}});
                      {{TAB4}}Connect("{{signalInfo.NativeName}}", {{backingCallableName}});
                      {{TAB3}}}
                      {{TAB3}}{{backingDelegateName}} += value;
                      {{TAB2}}}
                      {{TAB2}}remove
                      {{TAB2}}{
                      {{TAB3}}{{backingDelegateName}} -= value;
                      {{TAB3}}
                      {{TAB3}}if({{backingDelegateName}} == null)
                      {{TAB3}}{
                      {{TAB4}}Disconnect("{{signalInfo.NativeName}}", {{backingCallableName}});
                      {{TAB4}}{{backingCallableName}} = default;
                      {{TAB3}}}
                      {{TAB2}}}
                      {{TAB1}}}
                      """
                );
                builder.AppendLine();
            }
        }

        builder.AppendLine(
            """
            #endregion

            """
        );
    }
}