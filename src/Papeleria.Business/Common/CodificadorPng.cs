using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace Papeleria.Business.Common;

/// <summary>
/// Codificador PNG mínimo en escala de grises de 8 bits. Se implementa aquí para que
/// la generación de códigos de barras no dependa de System.Drawing ni de bibliotecas
/// nativas, y el ejecutable siga siendo autónomo y portátil.
/// </summary>
public static class CodificadorPng
{
    private static readonly byte[] Firma = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

    private static readonly uint[] TablaCrc = ConstruirTablaCrc();

    /// <summary>
    /// Convierte una matriz de píxeles en escala de grises (0 = negro, 255 = blanco)
    /// en un archivo PNG completo.
    /// </summary>
    public static byte[] CodificarEscalaDeGrises(byte[] pixeles, int ancho, int alto)
    {
        if (pixeles.Length != ancho * alto)
        {
            throw new ArgumentException(
                "El tamaño del búfer no coincide con las dimensiones indicadas.", nameof(pixeles));
        }

        using var salida = new MemoryStream();
        salida.Write(Firma, 0, Firma.Length);

        EscribirChunk(salida, "IHDR", ConstruirCabecera(ancho, alto));
        EscribirChunk(salida, "IDAT", ComprimirPixeles(pixeles, ancho, alto));
        EscribirChunk(salida, "IEND", Array.Empty<byte>());

        return salida.ToArray();
    }

    private static byte[] ConstruirCabecera(int ancho, int alto)
    {
        var cabecera = new byte[13];

        BinaryPrimitives.WriteInt32BigEndian(cabecera.AsSpan(0, 4), ancho);
        BinaryPrimitives.WriteInt32BigEndian(cabecera.AsSpan(4, 4), alto);

        cabecera[8] = 8;  // profundidad de bits
        cabecera[9] = 0;  // tipo de color: escala de grises
        cabecera[10] = 0; // método de compresión: deflate
        cabecera[11] = 0; // método de filtrado estándar
        cabecera[12] = 0; // sin entrelazado

        return cabecera;
    }

    private static byte[] ComprimirPixeles(byte[] pixeles, int ancho, int alto)
    {
        using var crudo = new MemoryStream((ancho + 1) * alto);

        for (var fila = 0; fila < alto; fila++)
        {
            crudo.WriteByte(0); // filtro «None» para cada línea de barrido
            crudo.Write(pixeles, fila * ancho, ancho);
        }

        using var comprimido = new MemoryStream();

        // ZLibStream añade la cabecera zlib y el Adler-32 que exige el formato PNG.
        using (var deflate = new ZLibStream(comprimido, CompressionLevel.Optimal, leaveOpen: true))
        {
            crudo.Position = 0;
            crudo.CopyTo(deflate);
        }

        return comprimido.ToArray();
    }

    private static void EscribirChunk(Stream destino, string tipo, byte[] datos)
    {
        Span<byte> longitud = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(longitud, datos.Length);
        destino.Write(longitud);

        var etiqueta = Encoding.ASCII.GetBytes(tipo);
        destino.Write(etiqueta, 0, etiqueta.Length);
        destino.Write(datos, 0, datos.Length);

        var crc = CalcularCrc(etiqueta, datos);
        Span<byte> checksum = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(checksum, crc);
        destino.Write(checksum);
    }

    private static uint CalcularCrc(byte[] etiqueta, byte[] datos)
    {
        var crc = 0xFFFFFFFFu;

        foreach (var b in etiqueta)
        {
            crc = TablaCrc[(crc ^ b) & 0xFF] ^ (crc >> 8);
        }

        foreach (var b in datos)
        {
            crc = TablaCrc[(crc ^ b) & 0xFF] ^ (crc >> 8);
        }

        return crc ^ 0xFFFFFFFFu;
    }

    private static uint[] ConstruirTablaCrc()
    {
        var tabla = new uint[256];

        for (uint i = 0; i < 256; i++)
        {
            var valor = i;

            for (var bit = 0; bit < 8; bit++)
            {
                valor = (valor & 1) != 0 ? 0xEDB88320u ^ (valor >> 1) : valor >> 1;
            }

            tabla[i] = valor;
        }

        return tabla;
    }
}
