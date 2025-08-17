using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ii.EighthSolitude
{
    public class BimProcessor
    {
        public List<(int r, int g, int b)> Palette = null!;

        public List<Image<Rgba32>> Process(string filePath)
        {
            var images = new List<Image<Rgba32>>();

            using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            using (var reader = new BinaryReader(stream))
            {
                var spriteCount = GetFrameCount(reader);
                reader.BaseStream.Seek(0, SeekOrigin.Begin);
                long fileSize = stream.Length;

                if (spriteCount > 5000)
                {
                    Console.WriteLine($"Warning: High sprite count detected in {filePath}: {spriteCount} sprites. This may indicate an issue with the file structure.");
                    return [];
                }

                // Read all frame offsets
                var offsets = new List<int>();
                for (var i = 0; i < spriteCount; i++)
                {
                    offsets.Add(reader.ReadInt32());
                }

                // Validation
                for (var i = 0; i < offsets.Count; i++)
                {
                    if (offsets[i] < 0 || offsets[i] >= fileSize)
                    {
                        Console.WriteLine($"Warning: Invalid offset {i}: {offsets[i]} (file size: {fileSize})");
                    }
                    if (i > 0 && offsets[i] <= offsets[i - 1])
                    {
                        Console.WriteLine($"Warning: Non-ascending offset {i}: {offsets[i]} <= {offsets[i - 1]}");
                    }
                }

                // Add the file size as the last offset so we have an end boundary
                offsets.Add((int)fileSize);

                // Extract each sprite
                for (var i = 0; i < spriteCount; i++)
                {
                    var currentOffset = offsets[i];
                    var nextOffset = offsets[i + 1];
                    var spriteSize = nextOffset - currentOffset;

                    if (spriteSize > 0 && currentOffset >= 0 && currentOffset < fileSize)
                    {
                        stream.Seek(currentOffset, SeekOrigin.Begin);

                        var actualSize = Math.Min(spriteSize, (int)(fileSize - currentOffset));
                        var spriteData = reader.ReadBytes(actualSize);

                        try
                        {
                            var image = ExtractImage(spriteData);
                            if (image != null)
                            {
                                images.Add(image);
                            }
                            else
                            {
                                Console.WriteLine($"Failed to extract sprite {i}: returned null");
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error extracting sprite {i} (offset: {currentOffset}, size: {actualSize}): {ex.Message}");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"Sprite {i} has invalid size or offset: size={spriteSize}, offset={currentOffset}");
                    }
                }
            }

            return images;
        }

        private Image<Rgba32> ExtractImage(byte[] spriteData)
        {
            if (spriteData.Length < 4)
            {
                Console.WriteLine($"Sprite data too small: {spriteData.Length} bytes");
                return null!;
            }

            using var stream = new MemoryStream(spriteData);
            using var reader = new BinaryReader(stream);

            ushort width = reader.ReadUInt16(); // width is often larger than it needs to be, but the only impact is a bunch of transparent space on the right-hand side of the image
            ushort height = reader.ReadUInt16();

            if (width == 0 || height == 0)
            {
                Console.WriteLine($"Invalid dimensions: {width}x{height}");
                return null!;
            }

            if (width > 10000 || height > 10000)
            {
                Console.WriteLine($"Dimensions too large: {width}x{height}");
                return null!;
            }

            // Images can contain direct pixel data they can used an index RLE-transparency scheme.
            // We determine the image type based on the amount of data matching height*width.
            var availableData = spriteData.Length - 4; // Subtract width/height bytes
            var isDirectPixelImageSprite = availableData >= width * height;

            stream.Seek(4, SeekOrigin.Begin);

            if (isDirectPixelImageSprite)
            {
                return ExtractDirectPixelImage(reader, width, height);
            }
            else
            {
                return ExtractTransparencyIndexImage(reader, width, height);
            }
        }

        private Image<Rgba32> ExtractDirectPixelImage(BinaryReader reader, ushort width, ushort height)
        {
            var image = new Image<Rgba32>(width, height);

            try
            {
                for (var y = 0; y < height; y++)
                {
                    for (var x = 0; x < width; x++)
                    {
                        if (reader.BaseStream.Position >= reader.BaseStream.Length)
                        {
                            Console.WriteLine($"Warning: Reached end of stream while reading solid sprite at position ({x}, {y})");
                            break;
                        }

                        var pixelValue = reader.ReadByte();
                        var alpha = pixelValue == 0 ? (byte)0 : (byte)255;
                        var color = GetColorFromPalette(pixelValue);
                        image[x, y] = new Rgba32(color.r, color.g, color.b, alpha);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading solid sprite data: {ex.Message}");
                throw;
            }

            return image;
        }

        private Image<Rgba32> ExtractTransparencyIndexImage(BinaryReader reader, ushort width, ushort height)
        {
            var image = new Image<Rgba32>(width, height);

            // Initialize the entire image to transparent
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    image[x, y] = new Rgba32(0, 0, 0, 0); // Transparent
                }
            }

            try
            {
                // Step 1: Read the run-length encoding data (alternating transparency count and pixel count)
                var rleData = new List<byte>();

                while (reader.BaseStream.Position < reader.BaseStream.Length - 1)
                {
                    var count = reader.ReadByte();
                    var value = reader.ReadByte();

                    if (value != 0)
                    {
                        // If we read non-zero data we've hit the start of pixel data, we need to back up to read this data as pixels
                        reader.BaseStream.Position -= 2;
                        break;
                    }

                    rleData.Add(count);
                }

                // Step 2: Read the actual pixel data
                var pixelData = new List<byte>();
                while (reader.BaseStream.Position < reader.BaseStream.Length)
                {
                    pixelData.Add(reader.ReadByte());
                }

                // Step 3: Process each row using RLE data
                var rleIndex = 0;
                var pixelIndex = 0;
                for (var y = 0; y < height; y++)
                {
                    var x = 0;
                    var pixelsUsedThisRow = 0;

                    // Check if we have RLE data for this row
                    if (rleIndex < rleData.Count)
                    {
                        var regionsInRow = rleData[rleIndex];
                        rleIndex++;

                        for (var region = 0; region < regionsInRow && rleIndex < rleData.Count; region++)
                        {
                            // Read transparency count
                            var transparentCount = rleData[rleIndex];
                            rleIndex++;

                            // Skip transparent pixels
                            x += transparentCount;

                            // Read pixel data count (if we have more RLE data)
                            if (rleIndex < rleData.Count)
                            {
                                var pixelCount = rleData[rleIndex];
                                rleIndex++;

                                // Fill pixels
                                for (var i = 0; i < pixelCount && x < width && pixelIndex < pixelData.Count; i++)
                                {
                                    var pixelValue = pixelData[pixelIndex];
                                    var alpha = pixelValue == 0 ? (byte)0 : (byte)255;
                                    var color = GetColorFromPalette(pixelValue);
                                    image[x, y] = new Rgba32(color.r, color.g, color.b, alpha);

                                    x++;
                                    pixelIndex++;
                                    pixelsUsedThisRow++;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading RLE sprite data: {ex.Message}");
                throw;
            }

            return image;
        }

        private (byte r, byte g, byte b) GetColorFromPalette(byte pixelValue)
        {
            if (Palette != null && pixelValue < Palette.Count)
            {
                var paletteColor = Palette[pixelValue];
                return ((byte)paletteColor.r, (byte)paletteColor.g, (byte)paletteColor.b);
            }
            
            // Default to a greyscale palette
            return (pixelValue, pixelValue, pixelValue);
        }

        private static int GetFrameCount(BinaryReader br)
        {
            br.BaseStream.Seek(-4, SeekOrigin.End);
            var frameCount = br.ReadInt32();
            if (frameCount == 0)
            {
                br.BaseStream.Seek(0, SeekOrigin.Begin);
                int firstOffset = br.ReadInt32();
                frameCount = firstOffset / 4;
            }
            return frameCount;
        }
    }
}