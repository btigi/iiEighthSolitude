namespace ii.EighthSolitude
{
    // BUILD.DAT    units produced by each building
    // VDEPEND.DAT  vehicle/unit unlocks per building
    // DEPEND.DAT   building tech-tree unlocks
    public class DependDatEntry
    {
        public string Key { get; set; } = string.Empty;
        public List<string> Values { get; set; } = [];
    }

    public class DependProcessor
    {
        public List<DependDatEntry> Read(string filename)
        {
            ArgumentNullException.ThrowIfNull(filename);

            var entries = new List<DependDatEntry>();
            foreach (var rawLine in File.ReadLines(filename))
            {
                var line = rawLine.Trim();
                if (line.Length == 0)
                {
                    continue;
                }

                var separatorIndex = line.IndexOf('=');
                if (separatorIndex <= 0)
                {
                    throw new InvalidDataException($"Invalid depend entry in {filename}: '{rawLine}'");
                }

                var key = line[..separatorIndex].Trim();
                if (key.Length == 0)
                {
                    throw new InvalidDataException($"Invalid depend entry in {filename}: '{rawLine}'");
                }

                var values = new List<string>();
                var payload = line[(separatorIndex + 1)..];
                if (payload.Length > 0)
                {
                    foreach (var token in payload.Split(','))
                    {
                        var value = token.Trim();
                        if (value.Length == 0)
                        {
                            throw new InvalidDataException($"Invalid depend entry in {filename}: '{rawLine}'");
                        }
                        values.Add(value);
                    }
                }

                entries.Add(new DependDatEntry
                {
                    Key = key,
                    Values = values,
                });
            }

            return entries;
        }

        public Dictionary<string, List<string>> ReadMerged(string filename)
        {
            return Merge(Read(filename));
        }

        public void Write(List<DependDatEntry> entries, string filename)
        {
            ArgumentNullException.ThrowIfNull(entries);
            ArgumentNullException.ThrowIfNull(filename);

            var lines = new string[entries.Count];
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry == null)
                {
                    throw new ArgumentException("Depend entry list cannot contain null entries.", nameof(entries));
                }

                ValidateToken(entry.Key, nameof(entries), "key");
                if (entry.Values == null)
                {
                    throw new ArgumentException("Depend entry values cannot be null.", nameof(entries));
                }

                foreach (var value in entry.Values)
                {
                    ValidateToken(value, nameof(entries), "value");
                }

                lines[i] = $"{entry.Key}={string.Join(',', entry.Values)}";
            }

            var content = lines.Length == 0 ? string.Empty : string.Join("\r\n", lines) + "\r\n";
            File.WriteAllText(filename, content);
        }

        public void Write(IReadOnlyDictionary<string, List<string>> entries, string filename)
        {
            ArgumentNullException.ThrowIfNull(entries);

            var list = new List<DependDatEntry>(entries.Count);
            foreach (var pair in entries)
            {
                list.Add(new DependDatEntry
                {
                    Key = pair.Key,
                    Values = pair.Value ?? [],
                });
            }

            Write(list, filename);
        }

        public static Dictionary<string, List<string>> Merge(IEnumerable<DependDatEntry> entries)
        {
            ArgumentNullException.ThrowIfNull(entries);

            var merged = new Dictionary<string, List<string>>();
            foreach (var entry in entries)
            {
                if (entry == null)
                {
                    throw new ArgumentException("Depend entry list cannot contain null entries.", nameof(entries));
                }
                if (entry.Key == null)
                {
                    throw new ArgumentException("Depend entry key cannot be null.", nameof(entries));
                }
                if (entry.Values == null)
                {
                    throw new ArgumentException("Depend entry values cannot be null.", nameof(entries));
                }

                if (!merged.TryGetValue(entry.Key, out var values))
                {
                    values = [];
                    merged[entry.Key] = values;
                }

                values.AddRange(entry.Values);
            }

            return merged;
        }

        private static void ValidateToken(string? token, string paramName, string kind)
        {
            if (string.IsNullOrEmpty(token))
            {
                throw new ArgumentException($"Depend entry {kind} cannot be null or empty.", paramName);
            }
            if (token.IndexOfAny(['=', ',', '\r', '\n']) >= 0)
            {
                throw new ArgumentException($"Depend entry {kind} cannot contain '=', ',', or newlines.", paramName);
            }
        }
    }
}
