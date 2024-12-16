using System.Linq;
using Godot;
using Godot.Collections;

namespace GDExtensionAPIGenerator;

internal static partial class CodeGenerator
{
    private readonly struct MethodInfo
    {
        public readonly string NativeName;
        public readonly Property ReturnValue;
        public readonly MethodFlags Flags;
        public readonly int Id = 0;
        public readonly Property[] Arguments;
        public readonly Variant[] DefaultArguments;

        public MethodInfo(Dictionary dictionary)
        {
            using var nameInfo = dictionary["name"];
            using var argsInfo = dictionary["args"];
            using var defaultArgsInfo = dictionary["default_args"];
            using var flagsInfo = dictionary["flags"];
            using var idInfo = dictionary["id"];
            using var returnInfo = dictionary["return"];

            NativeName = nameInfo.AsString();
            ReturnValue = new(returnInfo.As<Dictionary>());
            Flags = flagsInfo.As<MethodFlags>();
            Id = idInfo.AsInt32();
            Arguments = argsInfo.As<Array<Dictionary>>().Select(x => new Property(x)).ToArray();
            DefaultArguments = defaultArgsInfo.As<Array<Variant>>().ToArray();
        }

        public string GetMethodName() => EscapeAndFormatName(NativeName);

        public override string ToString() => $"""
                                              MethodInfo;
                                              {TAB1}{nameof(NativeName)}: {NativeName}, 
                                              {TAB1}{nameof(ReturnValue)}: {ReturnValue}, 
                                              {TAB1}{nameof(Flags)}: {Flags}, 
                                              {TAB1}{nameof(Id)}: {Id}, 
                                              {TAB1}{nameof(Arguments)}: 
                                              {TAB1}[
                                              {string.Join(",\n", Arguments.Select(x => TAB2 + x.ToString().ReplaceLineEndings($"\n{TAB2}")))}
                                              {TAB1}], 
                                              {TAB1}{nameof(DefaultArguments)}: 
                                              {TAB1}[
                                              {string.Join(",\n", DefaultArguments.Select(x => TAB2 + x.ToString().ReplaceLineEndings($"\n{TAB2}")))}
                                              {TAB1}]
                                              """;
    }
}