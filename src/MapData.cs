namespace ii.EighthSolitude
{
    public struct MapObject
    {
        public byte Type;
        public short X;
        public short Y;

        public bool IsEmpty => Type == 0;
    }

    public class MapData
    {
        public const int Width = MapProcessor.Width;
        public const int Height = MapProcessor.Height;
        public const int CellCount = Width * Height;
        public const int ObjectSlotCount = 3000;

        public int[] Terrain { get; set; } = new int[CellCount];
        public int[] Overlay { get; set; } = new int[CellCount];
        public byte[] Heightmap { get; set; } = new byte[CellCount];
        public byte[] Light { get; set; } = new byte[CellCount];
        public MapObject[] Objects { get; set; } = new MapObject[ObjectSlotCount];

        public static int Index(int x, int y) => x * Height + y;

        public static MapData CreateEmpty()
        {
            return new MapData();
        }
    }
}
