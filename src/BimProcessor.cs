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

        public void Write(string filename, IReadOnlyList<Image<Rgba32>> images)
        {
            ArgumentNullException.ThrowIfNull(filename);
            ArgumentNullException.ThrowIfNull(images);
            if (images.Count == 0)
            {
                throw new ArgumentException("At least one frame is required.", nameof(images));
            }

            if (images.Count > MaxFrameCount)
            {
                throw new ArgumentException($"BIM files support at most {MaxFrameCount} frames; got {images.Count}.", nameof(images));
            }

            if (Palette == null || Palette.Count == 0)
            {
                throw new InvalidOperationException("Palette must be set before writing BIM files.");
            }

            var frames = new List<byte[]>(images.Count);
            foreach (var image in images)
            {
                ArgumentNullException.ThrowIfNull(image);
                if (image.Width <= 0 || image.Height <= 0 || image.Width > MaxDimension || image.Height > MaxDimension)
                {
                    throw new ArgumentException($"Frame dimensions must be between 1 and {MaxDimension}; got {image.Width}x{image.Height}.", nameof(images));
                }

                frames.Add(EncodeFrame(image));
            }

            var indexLength = images.Count * 4;
            var totalSize = indexLength + frames.Sum(f => f.Length);
            var data = new byte[totalSize];

            var offset = indexLength;
            for (var i = 0; i < frames.Count; i++)
            {
                BitConverter.TryWriteBytes(data.AsSpan(i * 4, 4), offset);
                Buffer.BlockCopy(frames[i], 0, data, offset, frames[i].Length);
                offset += frames[i].Length;
            }

            File.WriteAllBytes(filename, data);
        }

        private byte[] EncodeFrame(Image<Rgba32> image)
        {
            var indices = Quantize(image);
            var uncompressed = EncodeUncompressedFrame(indices, image.Width, image.Height);
            var rle = EncodeRleFrame(indices, image.Width, image.Height);

            // Span-packed frames omit trailing transparent columns, so only use them when
            // the rightmost column has content (preserving width) and they shrink the payload
            if (rle != null &&
                rle.Length < uncompressed.Length &&
                HasOpaquePixelInColumn(indices, image.Width, image.Height, image.Width - 1))
            {
                return rle;
            }

            return uncompressed;
        }

        private static bool HasOpaquePixelInColumn(byte[] indices, int width, int height, int column)
        {
            for (var y = 0; y < height; y++)
            {
                if (indices[y * width + column] != 0)
                {
                    return true;
                }
            }

            return false;
        }

        private byte[] Quantize(Image<Rgba32> image)
        {
            var indices = new byte[image.Width * image.Height];
            var i = 0;
            for (var y = 0; y < image.Height; y++)
            {
                for (var x = 0; x < image.Width; x++)
                {
                    var pixel = image[x, y];
                    indices[i++] = pixel.A < 128 ? (byte)0 : FindNearestIndex(pixel.R, pixel.G, pixel.B);
                }
            }

            return indices;
        }

        private byte FindNearestIndex(byte r, byte g, byte b)
        {
            var count = Palette!.Count;
            // Index 0 = transparency
            if (count <= 1)
            {
                return 0;
            }

            var bestIndex = 1;
            var bestDistance = int.MaxValue;

            for (var i = 1; i < count; i++)
            {
                var (pr, pg, pb) = Palette[i];
                var dr = pr - r;
                var dg = pg - g;
                var db = pb - b;
                var distance = dr * dr + dg * dg + db * db;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestIndex = i;
                    if (distance == 0)
                    {
                        break;
                    }
                }
            }

            return (byte)bestIndex;
        }

        private static byte[] EncodeUncompressedFrame(byte[] indices, int width, int height)
        {
            var data = new byte[4 + indices.Length];
            BitConverter.TryWriteBytes(data.AsSpan(0, 2), (short)width);
            BitConverter.TryWriteBytes(data.AsSpan(2, 2), (short)height);
            Buffer.BlockCopy(indices, 0, data, 4, indices.Length);
            return data;
        }

        private static byte[]? EncodeRleFrame(byte[] indices, int width, int height)
        {
            var rowChunks = new List<(short X, short Count)>[height];
            var pixelBytes = new List<byte>(indices.Length);

            for (var y = 0; y < height; y++)
            {
                var chunks = new List<(short X, short Count)>();
                var rowOffset = y * width;
                var x = 0;
                while (x < width)
                {
                    while (x < width && indices[rowOffset + x] == 0)
                    {
                        x++;
                    }

                    if (x >= width)
                    {
                        break;
                    }

                    var start = x;
                    while (x < width && indices[rowOffset + x] != 0)
                    {
                        pixelBytes.Add(indices[rowOffset + x]);
                        x++;
                    }

                    var count = x - start;
                    if (count > short.MaxValue || start > short.MaxValue)
                    {
                        return null;
                    }

                    chunks.Add(((short)start, (short)count));
                }

                if (chunks.Count > short.MaxValue)
                {
                    return null;
                }

                rowChunks[y] = chunks;
            }

            var headerSize = 4;
            foreach (var chunks in rowChunks)
            {
                headerSize += 2 + chunks.Count * 4;
            }

            if (headerSize > short.MaxValue)
            {
                return null;
            }

            var data = new byte[headerSize + pixelBytes.Count];
            BitConverter.TryWriteBytes(data.AsSpan(0, 2), (short)headerSize);
            BitConverter.TryWriteBytes(data.AsSpan(2, 2), (short)height);

            var position = 4;
            foreach (var chunks in rowChunks)
            {
                BitConverter.TryWriteBytes(data.AsSpan(position, 2), (short)chunks.Count);
                position += 2;
                foreach (var (x, count) in chunks)
                {
                    BitConverter.TryWriteBytes(data.AsSpan(position, 2), x);
                    BitConverter.TryWriteBytes(data.AsSpan(position + 2, 2), count);
                    position += 4;
                }
            }

            pixelBytes.CopyTo(data.AsSpan(headerSize));
            return data;
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
