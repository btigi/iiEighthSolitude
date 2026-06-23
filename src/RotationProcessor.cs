namespace ii.EighthSolitude
{
    public enum RotationStorageFormat
    {
        Int16,
        Int32
    }

    public class RotationProcessor
    {
        private const int EntryCount = 2560;
        private const int Int16AxisFileSize = 5120;
        private const int Int32AxisFileSize = 10240;

        public RotationStorageFormat StorageFormat { get; set; } = RotationStorageFormat.Int16;

        public List<(int x, int y)> Read(string xFilename, string yFilename)
        {
            ArgumentNullException.ThrowIfNull(xFilename);
            ArgumentNullException.ThrowIfNull(yFilename);

            var xValues = ReadAxisFile(xFilename);
            var yValues = ReadAxisFile(yFilename);

            if (xValues.Count != yValues.Count)
            {
                throw new InvalidDataException($"Rotation axis files have mismatched entry counts: {xValues.Count} in '{xFilename}', {yValues.Count} in '{yFilename}'.");
            }

            var entries = new List<(int x, int y)>(xValues.Count);
            for (int i = 0; i < xValues.Count; i++)
            {
                entries.Add((xValues[i], yValues[i]));
            }

            return entries;
        }

        public void Write(List<(int x, int y)> entries, string xFilename, string yFilename)
        {
            ArgumentNullException.ThrowIfNull(entries);
            ArgumentNullException.ThrowIfNull(xFilename);
            ArgumentNullException.ThrowIfNull(yFilename);

            if (entries.Count != EntryCount)
            {
                throw new ArgumentException($"Rotation table must contain exactly {EntryCount} entries; got {entries.Count}.", nameof(entries));
            }

            WriteAxisFile(xFilename, entries.Select(e => e.x));
            WriteAxisFile(yFilename, entries.Select(e => e.y));
        }

        private List<int> ReadAxisFile(string filename)
        {
            using var stream = new FileStream(filename, FileMode.Open, FileAccess.Read);
            using var reader = new BinaryReader(stream);

            if (stream.Length != Int16AxisFileSize && stream.Length != Int32AxisFileSize)
            {
                throw new InvalidDataException($"Invalid rotation axis file size: {stream.Length} bytes in '{filename}'. Expected {Int16AxisFileSize} or {Int32AxisFileSize} bytes.");
            }

            var values = new List<int>(EntryCount);

            if (stream.Length == Int16AxisFileSize)
            {
                for (int i = 0; i < EntryCount; i++)
                {
                    values.Add(reader.ReadInt16());
                }
            }
            else
            {
                for (int i = 0; i < EntryCount; i++)
                {
                    values.Add(reader.ReadInt32());
                }
            }

            return values;
        }

        private void WriteAxisFile(string filename, IEnumerable<int> values)
        {
            using var stream = new FileStream(filename, FileMode.Create, FileAccess.Write);
            using var writer = new BinaryWriter(stream);

            if (StorageFormat == RotationStorageFormat.Int16)
            {
                foreach (int value in values)
                {
                    writer.Write((short)Math.Clamp(value, short.MinValue, short.MaxValue));
                }
            }
            else
            {
                foreach (int value in values)
                {
                    writer.Write(value);
                }
            }
        }
    }
}