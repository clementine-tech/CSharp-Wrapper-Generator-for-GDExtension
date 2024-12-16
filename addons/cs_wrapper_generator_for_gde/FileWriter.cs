using System.Collections.Generic;
using Godot;

namespace GDExtensionAPIGenerator;

internal static class FileWriter
{
    internal static void WriteResult(IReadOnlyList<(string filePath, string fileContent)> filesToCreate)
    {
        DirAccess.MakeDirAbsolute(GeneratorMain.WRAPPERS_PATH);
        
        foreach (var (filePath, fileContent) in filesToCreate)
        {
            if (fileContent is null) continue;
            using var fileAccess = FileAccess.Open(filePath, FileAccess.ModeFlags.Write);
            fileAccess.StoreString(fileContent);
        }

        EditorInterface
            .Singleton
            .GetResourceFilesystem()
            .Scan();
    }
}