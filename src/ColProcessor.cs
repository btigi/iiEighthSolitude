namespace ii.EighthSolitude
{
    public class ColProcessor
    {
        public List<(int r, int g, int b)> Process(string filename)
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
    }
}