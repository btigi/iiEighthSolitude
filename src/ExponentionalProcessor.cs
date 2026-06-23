namespace ii.EighthSolitude
{
    public class ExponentionalProcessor
    {
        private const int EntryCount = 65536;
        private const int FileSize = EntryCount * 4;

        public List<int> Read(string filename)
        {
            using var stream = new FileStream(filename, FileMode.Open, FileAccess.Read);
            using var reader = new BinaryReader(stream);

            if (stream.Length != FileSize)
            {
                throw new InvalidDataException($"Invalid exponential table file size: {stream.Length} bytes. Expected {FileSize} bytes.");
            }

            var values = new List<int>(EntryCount);
            for (int i = 0; i < EntryCount; i++)
            {
                values.Add((int)reader.ReadUInt32());
            }

            return values;
        }

        public void Write(List<int> entries, string filename)
        {
            ArgumentNullException.ThrowIfNull(entries);
            ArgumentNullException.ThrowIfNull(filename);

            if (entries.Count != EntryCount)
            {
                throw new ArgumentException($"Exponential table must contain exactly {EntryCount} entries; got {entries.Count}.", nameof(entries));
            }

            using var stream = new FileStream(filename, FileMode.Create, FileAccess.Write);
            using var writer = new BinaryWriter(stream);

            foreach (int value in entries)
            {
                writer.Write((uint)Math.Clamp(value, 0, 65535));
            }
        }
    }
}