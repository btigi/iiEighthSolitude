using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ii.EighthSolitude
{
    public class BimProcessor
    {
        private const int VclzSignature = unchecked((int)0x5a4c4356);
        private const int MaxDimension = 10000;
        private const int MaxFrameCount = 5000;
        private const int MaxUncompressedSize = 8 * 1024 * 1024;
        private const int LzssWindowSize = 0x1000;

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

        // VCLZ is a 4-byte magic, a 32-bit uncompressed length, then an LZSS bitstream
        // (12-bit window, flag-byte literals / 2-byte backreferences)
        private static byte[] TryDecompressVclz(byte[] fileBytes)
        {
            if (fileBytes.Length < 8)
            {
                return [];
            }

            var uncompressedSize = BitConverter.ToInt32(fileBytes, 4);
            if (uncompressedSize <= 0 || uncompressedSize > MaxUncompressedSize)
            {
                return [];
            }

            try
            {
                return DecompressLzss(fileBytes.AsSpan(8), uncompressedSize);
            }
            catch
            {
                return [];
            }
        }

        private static byte[] DecompressLzss(ReadOnlySpan<byte> compressed, int uncompressedSize)
        {
            var output = new byte[uncompressedSize];
            var window = new byte[LzssWindowSize];
            var windowIndex = 0;
            var src = 0;
            var dst = 0;

            while (dst < uncompressedSize)
            {
                if (src >= compressed.Length)
                {
                    break;
                }

                var flags = compressed[src++];
                for (var bit = 0; bit < 8 && dst < uncompressedSize; bit++)
                {
                    if (((flags >> bit) & 1) != 0)
                    {
                        if (src >= compressed.Length)
                        {
                            break;
                        }

                        var literal = compressed[src++];
                        window[windowIndex] = literal;
                        output[dst++] = literal;
                        windowIndex = (windowIndex + 1) % LzssWindowSize;
                    }
                    else
                    {
                        if (src + 1 >= compressed.Length)
                        {
                            break;
                        }

                        var meta0 = compressed[src++];
                        var meta1 = compressed[src++];
                        var offset = meta0 + ((meta1 & 0xF0) << 4) + 18;
                        var length = (meta1 & 0x0F) + 3;

                        for (var i = 0; i < length && dst < uncompressedSize; i++)
                        {
                            var value = window[(offset + i) % LzssWindowSize];
                            window[windowIndex] = value;
                            output[dst++] = value;
                            windowIndex = (windowIndex + 1) % LzssWindowSize;
                        }
                    }
                }
            }

            return dst == uncompressedSize ? output : [];
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
            {
                offsets[i] = BitConverter.ToInt32(data, i * 4);
            }

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

            var rawSize = 4 + widthOrPixelOffset * height;

            // An uncompressed frame is header + width * height indexed bytes, sometimes with a short trailer
            if (widthOrPixelOffset > 0 && widthOrPixelOffset <= MaxDimension && rawSize == frameData.Length)
            {
                return DecodeUncompressedFrame(frameData, widthOrPixelOffset, height);
            }

            var rle = DecodeRleFrame(frameData, widthOrPixelOffset, height);
            if (rle != null)
            {
                return rle;
            }

            // Padded uncompressed frames fail the previous check check so we handle them here
            if (widthOrPixelOffset > 0 && widthOrPixelOffset <= MaxDimension &&
                rawSize > 4 && rawSize <= frameData.Length &&
                frameData.Length - rawSize < widthOrPixelOffset * height)
            {
                return DecodeUncompressedFrame(frameData, widthOrPixelOffset, height);
            }

            return null;
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
            if (pixelOffset < 4 || pixelOffset > frameData.Length)
            {
                return null;
            }

            var rowChunks = new List<(int X, int Count)>[height];
            var position = 4;

            for (var y = 0; y < height; y++)
            {
                if (position + 2 > pixelOffset)
                {
                    return null;
                }

                var chunkCount = BitConverter.ToInt16(frameData, position);
                position += 2;
                if (chunkCount < 0)
                {
                    return null;
                }

                var chunks = new List<(int X, int Count)>(chunkCount);
                for (var i = 0; i < chunkCount; i++)
                {
                    if (position + 4 > pixelOffset)
                    {
                        return null;
                    }

                    var xOffset = BitConverter.ToInt16(frameData, position);
                    var count = BitConverter.ToInt16(frameData, position + 2);
                    position += 4;
                    chunks.Add((xOffset, count));
                }

                rowChunks[y] = chunks;
            }

            if (position != pixelOffset)
            {
                return null;
            }

            var pixels = frameData.AsSpan(pixelOffset);
            var rows = new byte[height][];
            var readOffset = 0;

            for (var y = 0; y < height; y++)
            {
                var row = Array.Empty<byte>();
                foreach (var (xOffset, count) in rowChunks[y])
                {
                    if (count <= 0 || xOffset < 0)
                    {
                        continue;
                    }

                    var copy = Math.Min(count, pixels.Length - readOffset);
                    if (copy <= 0)
                    {
                        continue;
                    }

                    Array.Resize(ref row, Math.Max(row.Length, xOffset + copy));
                    pixels.Slice(readOffset, copy).CopyTo(row.AsSpan(xOffset));
                    readOffset += copy;
                }

                rows[y] = row;
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
