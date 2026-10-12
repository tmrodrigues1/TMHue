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
        // Nome único: duas gravações simultâneas do mesmo destino não disputam o temporário.
        var tempPath = $"{filePath}.{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllText(tempPath, contents);

            if (File.Exists(filePath))
                File.Replace(tempPath, filePath, destinationBackupFileName: null);
            else
                File.Move(tempPath, filePath);
        }
        finally
        {
            // Só existe aqui se a gravação ou a promoção falhou.
            File.Delete(tempPath);
        }
    }
}
