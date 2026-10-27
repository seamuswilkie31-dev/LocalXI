using System.Buffers.Binary;
using System.Text;

namespace LocalXI.Core.Util;

/// <summary>Reads the names a Windows DLL exports, from its PE export table, without loading it.</summary>
public static class PeExports
{
    public static IReadOnlySet<string> Read(string path) => Read(File.ReadAllBytes(path));

    public static IReadOnlySet<string> Read(byte[] image)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        try
        {
            var pe = BinaryPrimitives.ReadInt32LittleEndian(image.AsSpan(0x3C));
            if (BinaryPrimitives.ReadUInt32LittleEndian(image.AsSpan(pe)) != 0x00004550) // "PE\0\0"
            {
                return names;
            }

            var sectionCount = BinaryPrimitives.ReadUInt16LittleEndian(image.AsSpan(pe + 6));
            var optionalSize = BinaryPrimitives.ReadUInt16LittleEndian(image.AsSpan(pe + 20));
            var optional = pe + 24;
            var is64 = BinaryPrimitives.ReadUInt16LittleEndian(image.AsSpan(optional)) == 0x20B;
            var exportDirectory = BinaryPrimitives.ReadInt32LittleEndian(image.AsSpan(optional + (is64 ? 112 : 96)));
            if (exportDirectory == 0)
            {
                return names;
            }

            var sections = Enumerable.Range(0, sectionCount)
                .Select(i => optional + optionalSize + i * 40)
                .Select(s => (Va: BinaryPrimitives.ReadInt32LittleEndian(image.AsSpan(s + 12)),
                              Size: Math.Max(BinaryPrimitives.ReadInt32LittleEndian(image.AsSpan(s + 8)), BinaryPrimitives.ReadInt32LittleEndian(image.AsSpan(s + 16))),
                              Raw: BinaryPrimitives.ReadInt32LittleEndian(image.AsSpan(s + 20))))
                .ToList();

            int Offset(int rva)
            {
                foreach (var (va, size, raw) in sections)
                {
                    if (rva >= va && rva < va + size)
                    {
                        return rva - va + raw;
                    }
                }

                throw new InvalidDataException("Address outside the image.");
            }

            var directory = Offset(exportDirectory);
            var nameCount = BinaryPrimitives.ReadInt32LittleEndian(image.AsSpan(directory + 24));
            var nameTable = Offset(BinaryPrimitives.ReadInt32LittleEndian(image.AsSpan(directory + 32)));
            for (var i = 0; i < nameCount; i++)
            {
                var start = Offset(BinaryPrimitives.ReadInt32LittleEndian(image.AsSpan(nameTable + 4 * i)));
                var end = Array.IndexOf(image, (byte)0, start);
                names.Add(Encoding.ASCII.GetString(image, start, end - start));
            }
        }
        catch (Exception e) when (e is ArgumentOutOfRangeException or IndexOutOfRangeException or InvalidDataException)
        {
            // Not a well-formed DLL: report no exports, which the caller treats as unusable.
        }

        return names;
    }
}
