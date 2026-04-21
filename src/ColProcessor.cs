namespace ii.EighthSolitude
{
    public class ColProcessor
    {
        public List<(int r, int g, int b)> Read(string filename)
        {
            var colors = new List<(int r, int g, int b)>();

            using var stream = new FileStream(filename, FileMode.Open, FileAccess.Read);
            using var reader = new BinaryReader(stream);

            if (stream.Length != 768)
            {
                throw new InvalidDataException($"Invalid COL file size: {stream.Length} bytes. Expected 768 bytes.");
            }

            for (int i = 0; i < 256; i++)
            {
                byte r = reader.ReadByte();
                byte g = reader.ReadByte();
                byte b = reader.ReadByte();

                colors.Add((r, g, b));
            }

            return colors;
        }

        public void Write(string filename, List<(int r, int g, int b)> colors)
        {
            ArgumentNullException.ThrowIfNull(colors);
            if (colors.Count != 256)
            {
                throw new ArgumentException($"COL palette must contain exactly 256 colors; got {colors.Count}.", nameof(colors));
            }

            using var stream = new FileStream(filename, FileMode.Create, FileAccess.Write);
            using var writer = new BinaryWriter(stream);

            for (int i = 0; i < 256; i++)
            {
                var (r, g, b) = colors[i];
                writer.Write((byte)Math.Clamp(r, 0, 255));
                writer.Write((byte)Math.Clamp(g, 0, 255));
                writer.Write((byte)Math.Clamp(b, 0, 255));
            }
        }
    }
}