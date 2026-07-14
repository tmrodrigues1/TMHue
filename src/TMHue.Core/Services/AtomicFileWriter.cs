namespace TMHue.Core.Services;

/// <summary>
/// Grava texto de forma atômica: escreve em um arquivo temporário ao lado do destino e o
/// promove com File.Replace/Move. Uma queda de energia no meio da gravação deixa o arquivo
/// anterior intacto em vez de um JSON truncado.
/// </summary>
public static class AtomicFileWriter
{
    public static void Write(string filePath, string contents)
    {
        var tempPath = filePath + ".tmp";
        File.WriteAllText(tempPath, contents);

        if (File.Exists(filePath))
            File.Replace(tempPath, filePath, destinationBackupFileName: null);
        else
            File.Move(tempPath, filePath);
    }
}
