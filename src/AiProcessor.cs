namespace ii.EighthSolitude
{
    public class AiProcessor
    {
        public List<(string unit, int x, int y)> Read(string filename)
        {
            var entries = new List<(string unit, int x, int y)>();

            foreach (var line in File.ReadLines(filename))
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                var separatorIndex = line.IndexOf('=');
                if (separatorIndex <= 0 || separatorIndex == line.Length - 1)
                {
                    throw new InvalidDataException($"Invalid AI entry in {filename}: '{line}'");
                }

                var unit = line[..separatorIndex];
                var location = line[(separatorIndex + 1)..];
                var (x, y) = ParseLocation(location, filename, line);
                entries.Add((unit, x, y));
            }

            return entries;
        }

        public void Write(List<(string unit, int x, int y)> entries, string filename)
        {
            ArgumentNullException.ThrowIfNull(entries);

            var lines = new string[entries.Count];
            for (var i = 0; i < entries.Count; i++)
            {
                var (unit, x, y) = entries[i];
                if (string.IsNullOrEmpty(unit))
                {
                    throw new ArgumentException("AI entry unit cannot be null or empty.", nameof(entries));
                }
                if (y < 0 || y > 0xFF)
                {
                    throw new ArgumentException($"AI entry y must be between 0 and 255; got {y}.", nameof(entries));
                }
                if (x < 0)
                {
                    throw new ArgumentException($"AI entry x cannot be negative; got {x}.", nameof(entries));
                }

                lines[i] = $"{unit}={FormatLocation(x, y)}";
            }

            var content = lines.Length == 0 ? string.Empty : string.Join('\n', lines) + '\n';
            File.WriteAllText(filename, content);
        }

        private static (int x, int y) ParseLocation(string location, string filename, string line)
        {
            if (location.Length < 2)
            {
                throw new InvalidDataException($"Invalid AI location in {filename}: '{line}'");
            }

            var yPart = location[^2..];
            var xPart = location.Length > 2 ? location[..^2] : "0";

            if (!int.TryParse(yPart, System.Globalization.NumberStyles.HexNumber, null, out var y))
                throw new InvalidDataException($"Invalid AI location in {filename}: '{line}'");

            if (!int.TryParse(xPart, System.Globalization.NumberStyles.HexNumber, null, out var x))
                throw new InvalidDataException($"Invalid AI location in {filename}: '{line}'");

            return (x, y);
        }

        private static string FormatLocation(int x, int y)
        {
            return x == 0 ? y.ToString("x2") : $"{x:x}{y:x2}";
        }
    }
}