namespace ii.EighthSolitude
{
	public class DatProcessor
	{
		public List<(int index, List<(int r, int g, int b)>)> Process(string filename)
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
	}
}