using System.Linq;
using Godot;

#if TOOLS

namespace GDExtensionAPIGenerator;

internal static class GeneratorMain
{
    /// <summary>
    /// TODO: User configurable wrappers path? 
    /// </summary>
    public const string WRAPPERS_PATH = "res://GDExtensionWrappers/";
    
    const string WRAPPERS_EXT = ".cs";

    /// <summary>
    /// Gets the full path (starts from res://) for the given type name.
    /// </summary>
    static string GetWrapperPath(string typeName) => 
        WRAPPERS_PATH + typeName + WRAPPERS_EXT;

    /// <summary>
    /// Core Generator logic
    /// </summary>
    public static void Generate()
    {
        // Launch the Godot Editor and dump all builtin types and GDExtension types.
        if(!TypeCollector.TryCollectGDExtensionTypes(out var gdeClassTypes, out var builtinTypeNames)) return;
        
        // Generate source codes for the GDExtension types.
        var filesToCreate = CodeGenerator
            .GenerateWrappersForGDETypes(gdeClassTypes, builtinTypeNames)
            .Select(x => (GetWrapperPath(x.typeName), x.fileContent))
            .ToList();

        filesToCreate.Add(GetEditorConfigFile());
        
        // Write the generated result to the filesystem, and call update.
        FileWriter.WriteResult(filesToCreate);
        
        // Print the result.
        GD.Print($"Finish generating wrappers for the following classes: \n{string.Join('\n', gdeClassTypes)}");
    }
    
    static (string filePath, string fileContent) GetEditorConfigFile()
    {
        const string fileContent =
            """
            [*.cs]
            root = true
            generated_code = true
            dotnet_analyzer_diagnostic.severity = none
            """;

        return (WRAPPERS_PATH + ".editorconfig", fileContent);
    }
}
#endif