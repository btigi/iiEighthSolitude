using System.IO.Compression;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ii.EighthSolitude
{
    public class BimProcessor
    {
        private const int VclzSignature = unchecked((int)0x5a4c4356);
        private const int MaxDimension = 10000;
        private const int MaxFrameCount = 5000;

        public List<(int r, int g, int b)>? Palette { get; set; }

        public List<Image<Rgba32>> Read(string filename)
        {
            var fileBytes = File.ReadAllBytes(filename);
            if (fileBytes.Length < 4)
            {
                return [];
            }

            if (BitConverter.ToInt32(fileBytes, 0) == VclzSignature)
            {
                fileBytes = TryDecompressVclz(fileBytes);
                if (fileBytes.Length == 0)
                {
                    return [];
                }
            }

            var frames = ParseFrameIndex(fileBytes);
            if (frames.Count > MaxFrameCount)
            {
                return [];
            }

            var images = new List<Image<Rgba32>>(frames.Count);
            foreach (var (offset, length) in frames)
            {
                if (length <= 0 || offset < 0 || offset >= fileBytes.Length)
                {
                    continue;
                }

                var size = Math.Min(length, fileBytes.Length - offset);
                var frameData = new byte[size];
                Buffer.BlockCopy(fileBytes, offset, frameData, 0, size);

                var image = DecodeFrame(frameData);
                if (image != null)
                {
                    images.Add(image);
                }
            }

            return images;
        }

        private static byte[] TryDecompressVclz(byte[] fileBytes)
        {
            var payload = new byte[fileBytes.Length - 4];
            Buffer.BlockCopy(fileBytes, 4, payload, 0, payload.Length);

            return Inflate(payload, input => new ZLibStream(input, CompressionMode.Decompress))
                ?? Inflate(payload, input => new DeflateStream(input, CompressionMode.Decompress))
                ?? [];
        }

        private static byte[]? Inflate(byte[] payload, Func<Stream, Stream> decompressorFactory)
        {
            try
            {
                using var input = new MemoryStream(payload, writable: false);
                using var decompressor = decompressorFactory(input);
                using var output = new MemoryStream();
                decompressor.CopyTo(output);
                return output.ToArray();
            }
            catch
            {
                return null;
            }
        }

        private static List<(int Offset, int Length)> ParseFrameIndex(byte[] data)
        {
            var frames = new List<(int Offset, int Length)>();
            if (data.Length < 4)
            {
                return frames;
            }

            var indexLength = BitConverter.ToInt32(data, 0);
            if (indexLength <= 0 || indexLength > data.Length)
            {
                return frames;
            }

            var entryCount = indexLength / 4;
            var offsets = new int[entryCount];
            for (var i = 0; i < entryCount; i++)
                offsets[i] = BitConverter.ToInt32(data, i * 4);

            for (var i = 0; i < entryCount; i++)
            {
                var start = offsets[i];

                // A frame runs until the next offset that actually advances. Repeated offsets
                // represent empty frames, and the last frame extends to the end of the file
                var end = data.Length;
                for (var next = i + 1; next < entryCount; next++)
                {
                    if (offsets[next] != start)
                    {
                        end = offsets[next];
                        break;
                    }
                }

                // The table may close with a frame-count value instead of an offset; ignore it
                if (start != data.Length - 4)
                {
                    frames.Add((start, end - start));
                }
            }

            return frames;
        }

        private Image<Rgba32>? DecodeFrame(byte[] frameData)
        {
            if (frameData.Length < 4)
            {
                return null;
            }

            // First field is the width for uncompressed frames OR the byte offset to the packed pixel data for run-length encoded frames
            var widthOrPixelOffset = BitConverter.ToInt16(frameData, 0);
            var height = BitConverter.ToInt16(frameData, 2);

            if (height <= 0 || height > MaxDimension)
            {
                return null;
            }

            // An uncompressed frame is exactly header + width * height indexed bytes
            if (frameData.Length == 4 + widthOrPixelOffset * height)
            {
                if (widthOrPixelOffset <= 0 || widthOrPixelOffset > MaxDimension)
                {
                    return null;
                }

                return DecodeUncompressedFrame(frameData, widthOrPixelOffset, height);
            }

            return DecodeRleFrame(frameData, widthOrPixelOffset, height);
        }

        private Image<Rgba32> DecodeUncompressedFrame(byte[] frameData, int width, int height)
        {
            var image = new Image<Rgba32>(width, height);
            var offset = 4;

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width && offset < frameData.Length; x++)
                {
                    image[x, y] = ToPixel(frameData[offset++]);
                }
            }

            return image;
        }

        private Image<Rgba32>? DecodeRleFrame(byte[] frameData, int pixelOffset, int height)
        {
            if (pixelOffset < 0 || pixelOffset > frameData.Length)
            {
                return null;
            }

            var pixels = frameData.AsSpan(pixelOffset);
            var rows = new byte[height][];
            var readOffset = 0;
            var position = 4;

            for (var i = 0; i < height; i++)
            {
                if (position + 2 > frameData.Length)
                {
                    rows[i] = [];
                    continue;
                }

                var chunkCount = BitConverter.ToInt16(frameData, position);
                position += 2;

                var row = Array.Empty<byte>();
                for (var j = 0; j < chunkCount; j++)
                {
                    if (position + 4 > frameData.Length)
                    {
                        break;
                    }

                    var xOffset = BitConverter.ToInt16(frameData, position);
                    var count = BitConverter.ToInt16(frameData, position + 2);
                    position += 4;

                    if (count <= 0 || xOffset < 0 || readOffset + count > pixels.Length)
                    {
                        continue;
                    }

                    Array.Resize(ref row, Math.Max(row.Length, xOffset + count));
                    pixels.Slice(readOffset, count).CopyTo(row.AsSpan(xOffset));
                    readOffset += count;
                }

                rows[i] = row;
            }

            var width = rows.Length == 0 ? 0 : rows.Max(r => r.Length);
            if (width <= 0 || width > MaxDimension)
            {
                return null;
            }

            var image = new Image<Rgba32>(width, height);
            for (var y = 0; y < height; y++)
            {
                var row = rows[y];
                for (var x = 0; x < row.Length; x++)
                {
                    image[x, y] = ToPixel(row[x]);
                }
            }

            return image;
        }

        private Rgba32 ToPixel(byte index)
        {
            var (r, g, b) = GetColorFromPalette(index);
            var alpha = index == 0 ? (byte)0 : (byte)255;
            return new Rgba32(r, g, b, alpha);
        }

        private (byte r, byte g, byte b) GetColorFromPalette(byte index)
        {
            if (Palette != null && index < Palette.Count)
            {
                var (r, g, b) = Palette[index];
                return ((byte)r, (byte)g, (byte)b);
            }

            return (index, index, index);
        }
    }
}
