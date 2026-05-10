using System;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace SGE
{
    public enum ItemType
    {
        Unknown = 0,
        Texture = 1,
        ScriptPython = 2,
        ScriptCSharp = 3,
        ScriptCpp = 4,
        Scene = 5,
        Audio = 6,
        Raw = 7,
        Model = 8
    }

    public sealed class AssetItem
    {
        public string Name { get; init; } = string.Empty;
        public ItemType Type { get; init; } = ItemType.Unknown;
        public byte[] Payload { get; init; } = Array.Empty<byte>();
        public string Metadata { get; init; } = string.Empty;

        public string GetText() => Encoding.UTF8.GetString(Payload);
        public static AssetItem CreateText(string name, ItemType type, string text) => new AssetItem
        {
            Name = name,
            Type = type,
            Payload = Encoding.UTF8.GetBytes(text)
        };
    }

    public static class ItemFile
    {
        private static readonly byte[] Header = Encoding.ASCII.GetBytes("CPRX7");

        public static void Save(string path, AssetItem item)
        {
            using var stream = File.Create(path);
            stream.Write(Header, 0, Header.Length);
            stream.WriteByte((byte)item.Type);
            var nameBytes = Encoding.UTF8.GetBytes(item.Name);
            stream.Write(BitConverter.GetBytes(nameBytes.Length));
            stream.Write(nameBytes);
            var metadataBytes = Encoding.UTF8.GetBytes(item.Metadata);
            stream.Write(BitConverter.GetBytes(metadataBytes.Length));
            stream.Write(metadataBytes);
            var compressed = Compress(item.Payload);
            stream.Write(BitConverter.GetBytes(compressed.Length));
            stream.Write(compressed);
        }

        public static AssetItem Load(string path)
        {
            using var stream = File.OpenRead(path);
            var header = new byte[Header.Length];
            stream.ReadExactly(header);
            if (!header.AsSpan().SequenceEqual(Header))
            {
                throw new InvalidDataException("Invalid CPRX7 header.");
            }

            var itemType = (ItemType)stream.ReadByte();
            var nameLength = ReadInt(stream);
            var nameBytes = new byte[nameLength];
            stream.ReadExactly(nameBytes);
            var metadataLength = ReadInt(stream);
            var metadataBytes = new byte[metadataLength];
            stream.ReadExactly(metadataBytes);
            var compressedLength = ReadInt(stream);
            var compressed = new byte[compressedLength];
            stream.ReadExactly(compressed);
            var payload = Decompress(compressed);

            return new AssetItem
            {
                Name = Encoding.UTF8.GetString(nameBytes),
                Type = itemType,
                Payload = payload,
                Metadata = Encoding.UTF8.GetString(metadataBytes)
            };
        }

        private static int ReadInt(Stream stream)
        {
            var bytes = new byte[4];
            stream.ReadExactly(bytes);
            return BitConverter.ToInt32(bytes);
        }

        private static byte[] Compress(byte[] data)
        {
            using var output = new MemoryStream();
            using var compressor = new DeflateStream(output, CompressionLevel.Optimal, true);
            compressor.Write(data);
            compressor.Close();
            return output.ToArray();
        }

        private static byte[] Decompress(byte[] data)
        {
            using var input = new MemoryStream(data);
            using var decompressor = new DeflateStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            decompressor.CopyTo(output);
            return output.ToArray();
        }
    }
}
