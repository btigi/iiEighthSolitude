namespace ii.EighthSolitude
{
	public class DatProcessor
	{
		public List<(int index, List<(int r, int g, int b)>)> Read(string filename)
		{
			var colors = new List<(int index, List<(int r, int g, int b)>)>();

			using var stream = new FileStream(filename, FileMode.Open, FileAccess.Read);
			using var reader = new BinaryReader(stream);

			int index = 0;
			while (reader.BaseStream.Position != reader.BaseStream.Length)
			{
				var palette = new List<(int r, int g, int b)>();
				for (int i = 0; i < 256; i++)
				{
					byte r = reader.ReadByte();
					byte g = reader.ReadByte();
					byte b = reader.ReadByte();

					palette.Add((r, g, b));
				}
				colors.Add((index, palette));
				index++;
			}

			return colors;
		}

		public void Write(string filename, List<(int index, List<(int r, int g, int b)>)> palettes)
		{
            ArgumentNullException.ThrowIfNull(palettes);

            using var stream = new FileStream(filename, FileMode.Create, FileAccess.Write);
			using var writer = new BinaryWriter(stream);

			foreach (var (_, palette) in palettes)
			{
				if (palette == null)
					throw new ArgumentException("Palette list cannot contain null entries.", nameof(palettes));
				if (palette.Count != 256)
					throw new ArgumentException($"Each DAT palette must contain exactly 256 colors; got {palette.Count}.", nameof(palettes));

				for (int i = 0; i < 256; i++)
				{
					var (r, g, b) = palette[i];
					writer.Write((byte)Math.Clamp(r, 0, 255));
					writer.Write((byte)Math.Clamp(g, 0, 255));
					writer.Write((byte)Math.Clamp(b, 0, 255));
				}
			}
		}
	}
}