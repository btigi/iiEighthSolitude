namespace ii.EighthSolitude
{
    public class C24Processor
    {
        private const int ColorCount = 256;
        private const int BytesPerEntry = 4;
        private const int FileSize = ColorCount * BytesPerEntry;

        public List<(int r, int g, int b)> Read(string filename)
        {
            var colors = new List<(int r, int g, int b)>(ColorCount);

            using var stream = new FileStream(filename, FileMode.Open, FileAccess.Read);
            using var reader = new BinaryReader(stream);

            if (stream.Length != FileSize)
            {
                throw new InvalidDataException($"Invalid C24 file size: {stream.Length} bytes. Expected {FileSize} bytes.");
            }

            for (int i = 0; i < ColorCount; i++)
            {
                byte r = reader.ReadByte();
                byte g = reader.ReadByte();
                byte b = reader.ReadByte();
                reader.ReadByte(); // padding, always 0

                colors.Add((r, g, b));
            }

            return colors;
        }

        public void Write(string filename, List<(int r, int g, int b)> colors)
        {
            ArgumentNullException.ThrowIfNull(colors);
            if (colors.Count != ColorCount)
            {
                throw new ArgumentException($"C24 palette must contain exactly {ColorCount} colors; got {colors.Count}.", nameof(colors));
            }

            using var stream = new FileStream(filename, FileMode.Create, FileAccess.Write);
            using var writer = new BinaryWriter(stream);

            for (int i = 0; i < ColorCount; i++)
            {
                var (r, g, b) = colors[i];
                writer.Write((byte)Math.Clamp(r, 0, 63));
                writer.Write((byte)Math.Clamp(g, 0, 63));
                writer.Write((byte)Math.Clamp(b, 0, 63));
                writer.Write((byte)0);
            }
        }

        // Palette components are 6-bit (0-63) therefore we scale them to the full 8-bit (0-255) range for display/PNG output
        public static List<(int r, int g, int b)> ScaleTo8Bit(List<(int r, int g, int b)> palette)
        {
            ArgumentNullException.ThrowIfNull(palette);

            static int Scale(int v)
            {
                v = Math.Clamp(v, 0, 63);
                return (v << 2) | (v >> 4);
            }

            var scaled = new List<(int r, int g, int b)>(palette.Count);
            foreach (var (r, g, b) in palette)
            {
                scaled.Add((Scale(r), Scale(g), Scale(b)));
            }

            return scaled;
        }
    }
}