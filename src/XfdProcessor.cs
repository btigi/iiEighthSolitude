namespace ii.EighthSolitude
{
    public class XfdProcessor
    {
        private const int ColorCount = 256;
        private const int ChannelCount = 64;
        private const int FileSize = ChannelCount * ChannelCount * ChannelCount;

        public byte[] Read(string filename)
        {
            using var stream = new FileStream(filename, FileMode.Open, FileAccess.Read);
            using var reader = new BinaryReader(stream);

            if (stream.Length != FileSize)
            {
                throw new InvalidDataException($"Invalid XFD file size: {stream.Length} bytes. Expected {FileSize} bytes.");
            }

            return reader.ReadBytes(FileSize);
        }

        public void Write(string xfdPath, List<(int r, int g, int b)> palette)
        {
            ArgumentNullException.ThrowIfNull(palette);
            if (palette.Count != ColorCount)
            {
                throw new ArgumentException($"XFD palette must contain exactly {ColorCount} colors; got {palette.Count}.", nameof(palette));
            }

            var lookup = BuildInverseLookup(palette);

            using var stream = new FileStream(xfdPath, FileMode.Create, FileAccess.Write);
            stream.Write(lookup, 0, lookup.Length);
        }

        public static byte[] BuildInverseLookup(List<(int r, int g, int b)> palette)
        {
            ArgumentNullException.ThrowIfNull(palette);
            if (palette.Count != ColorCount)
            {
                throw new ArgumentException($"XFD palette must contain exactly {ColorCount} colors; got {palette.Count}.", nameof(palette));
            }

            var paletteR = new int[ColorCount];
            var paletteG = new int[ColorCount];
            var paletteB = new int[ColorCount];

            for (int i = 0; i < ColorCount; i++)
            {
                var (r, g, b) = palette[i];
                paletteR[i] = Math.Clamp(r, 0, 63);
                paletteG[i] = Math.Clamp(g, 0, 63);
                paletteB[i] = Math.Clamp(b, 0, 63);
            }

            var lookup = new byte[FileSize];
            int offset = 0;

            for (int r = 0; r < ChannelCount; r++)
            {
                for (int g = 0; g < ChannelCount; g++)
                {
                    for (int b = 0; b < ChannelCount; b++)
                    {
                        lookup[offset++] = FindNearestIndex(r, g, b, paletteR, paletteG, paletteB);
                    }
                }
            }

            return lookup;
        }

        private static byte FindNearestIndex(int r, int g, int b, int[] paletteR, int[] paletteG, int[] paletteB)
        {
            int bestIndex = 0;
            int bestDistance = int.MaxValue;

            for (int i = 0; i < ColorCount; i++)
            {
                int dr = paletteR[i] - r;
                int dg = paletteG[i] - g;
                int db = paletteB[i] - b;
                int distance = dr * dr + dg * dg + db * db;

                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestIndex = i;
                }
            }

            return (byte)bestIndex;
        }
    }
}