using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ii.EighthSolitude
{
    /*
     Each level is a set of parallel MAP* files sharing the extension NNN (the Map= number in Missions.ini)
     Tile graphics come from the per-terrain block set (TILES*.BIM) coloured with the matching PAL?.COL palette
     Terrain= in Missions.ini: 0=desert/TILES.BIM/PAL1, 1=winter/TILES1/PAL2, 2=grassland/TILES2/PAL3, 3=indoor/TILES3/PAL4

    A level is a fixed 128x128 grid, stored column-wise, split across raw header-less files:

    MAPT    int16  Terrain tile index into the block set (`TILES*.BIM` frame number) - obfuscated
    MAPOVL  int16  Overlay: low byte = overlay type (`0` = none), high byte = animation state (0..31)
    MAPO    byte   Heightmap `0..16`
    MAPL    byte   Lightmap - obfuscated
    MAPCT   byte   Object type (`0` = empty)
    MAPCX   int16  Object X (tile coords)
    MAPCY   int16  Object X (tile coords)

    MAPT and MAPL are obfuscated
    A 16-bit key starts at `0x7530` (30000) and decrements once per cell in file order (column-wise) across the whole grid:
    tileIndex = ((fileValue ^ key) - y) & 0xFFFF   // MAPT (int16/cell)
    light = fileValue ^ x                          // MAPL (byte/cell)
    */
    public class MapProcessor
    {
        public const int Width = 128;
        public const int Height = 128;
        public const int TileSize = 32;
        public const int ObjectSlotCount = MapData.ObjectSlotCount;

        private const int MaxHeight = 16;
        private const int ObfuscationKeySeed = 0x7530;

        public MapData ReadMapData(string dataFolder, int mapNumber)
        {
            ArgumentNullException.ThrowIfNull(dataFolder);
            var ext = mapNumber.ToString("000");
            return ReadMapData(
                Path.Combine(dataFolder, $"MAPT.{ext}"),
                Path.Combine(dataFolder, $"MAPOVL.{ext}"),
                Path.Combine(dataFolder, $"MAPO.{ext}"),
                Path.Combine(dataFolder, $"MAPL.{ext}"),
                Path.Combine(dataFolder, $"MAPCT.{ext}"),
                Path.Combine(dataFolder, $"MAPCX.{ext}"),
                Path.Combine(dataFolder, $"MAPCY.{ext}"));
        }

        public MapData ReadMapData(string mapTPath, string mapOvlPath, string mapOPath, string mapLPath,
                                   string mapCtPath, string mapCxPath, string mapCyPath)
        {
            var map = new MapData
            {
                Terrain = ReadTerrain(mapTPath),
                Overlay = ReadCells16(mapOvlPath),
                Heightmap = ReadCells8(mapOPath),
                Light = ReadLight(mapLPath),
                Objects = ReadObjects(mapCtPath, mapCxPath, mapCyPath),
            };
            return map;
        }

        public void WriteMapData(MapData map, string dataFolder, int mapNumber)
        {
            ArgumentNullException.ThrowIfNull(map);
            ArgumentNullException.ThrowIfNull(dataFolder);
            var ext = mapNumber.ToString("000");
            WriteMapData(
                map,
                Path.Combine(dataFolder, $"MAPT.{ext}"),
                Path.Combine(dataFolder, $"MAPOVL.{ext}"),
                Path.Combine(dataFolder, $"MAPO.{ext}"),
                Path.Combine(dataFolder, $"MAPL.{ext}"),
                Path.Combine(dataFolder, $"MAPCT.{ext}"),
                Path.Combine(dataFolder, $"MAPCX.{ext}"),
                Path.Combine(dataFolder, $"MAPCY.{ext}"));
        }

        public void WriteMapData(MapData map, string mapTPath, string mapOvlPath, string mapOPath, string mapLPath,
                                 string mapCtPath, string mapCxPath, string mapCyPath)
        {
            ArgumentNullException.ThrowIfNull(map);
            ValidateMapArrays(map);

            WriteTerrain(map.Terrain, mapTPath);
            WriteCells16(map.Overlay, mapOvlPath);
            WriteCells8(map.Heightmap, mapOPath);
            WriteLight(map.Light, mapLPath);
            WriteObjects(map.Objects, mapCtPath, mapCxPath, mapCyPath);
        }

        public Image<Rgba32> ReadMap(string mapTPath, IEnumerable<string> bimPaths, IReadOnlyList<(int r, int g, int b)> palette, string? mapOvlPath = null,
                                     string? mapCtPath = null, string? mapCxPath = null, string? mapCyPath = null, bool drawObjectMarkers = true)
        {
            ArgumentNullException.ThrowIfNull(bimPaths);
            ArgumentNullException.ThrowIfNull(palette);

            var tiles = LoadTiles(bimPaths, palette);
            var terrain = ReadTerrain(mapTPath);

            var image = new Image<Rgba32>(Width * TileSize, Height * TileSize);

            for (var ty = 0; ty < Height; ty++)
            {
                for (var tx = 0; tx < Width; tx++)
                {
                    var index = terrain[tx * Height + ty];
                    if (index < 0 || index >= tiles.Count)
                    {
                        continue;
                    }

                    BlitTile(image, tiles[index], tx * TileSize, ty * TileSize);
                }
            }

            if (mapOvlPath != null)
            {
                DrawOverlayMarkers(image, mapOvlPath);
            }

            if (drawObjectMarkers && mapCtPath != null && mapCxPath != null && mapCyPath != null)
            {
                DrawObjects(image, mapCtPath, mapCxPath, mapCyPath);
            }

            return image;
        }

        // Greyscale heightmap (one pixel per tile). MAPO holds elevation 0..16, scaled to 0..255
        public Image<L8> ReadHeightMap(string mapOPath)
        {
            var data = ReadCells8(mapOPath);
            var image = new Image<L8>(Width, Height);
            for (var y = 0; y < Height; y++)
            {
                for (var x = 0; x < Width; x++)
                {
                    var v = data[x * Height + y];
                    var g = (byte)Math.Clamp(v * 255 / MaxHeight, 0, 255);
                    image[x, y] = new L8(g);
                }
            }

            return image;
        }

        // Greyscale lightmap (one pixel per tile)
        public Image<L8> ReadLightMap(string mapLPath)
        {
            var data = ReadCells8(mapLPath);
            var image = new Image<L8>(Width, Height);
            for (var y = 0; y < Height; y++)
            {
                for (var x = 0; x < Width; x++)
                {
                    image[x, y] = new L8((byte)(data[x * Height + y] ^ x));
                }
            }

            return image;
        }

        public static int[] ReadTerrain(string mapTPath)
        {
            var cells = ReadCells16(mapTPath);
            var key = ObfuscationKeySeed;
            for (var x = 0; x < Width; x++)
            {
                for (var y = 0; y < Height; y++)
                {
                    var i = x * Height + y;
                    cells[i] = ((cells[i] ^ key) - y) & 0xFFFF;
                    key = (key - 1) & 0xFFFF;
                }
            }

            return cells;
        }

        public static void WriteTerrain(int[] terrain, string mapTPath)
        {
            ArgumentNullException.ThrowIfNull(terrain);
            if (terrain.Length != Width * Height)
            {
                throw new ArgumentException($"Terrain array must contain {Width * Height} cells; got {terrain.Length}.", nameof(terrain));
            }

            var data = new byte[Width * Height * 2];
            var key = ObfuscationKeySeed;
            for (var x = 0; x < Width; x++)
            {
                for (var y = 0; y < Height; y++)
                {
                    var i = x * Height + y;
                    var encoded = ((terrain[i] + y) & 0xFFFF) ^ key;
                    data[i * 2] = (byte)(encoded & 0xFF);
                    data[i * 2 + 1] = (byte)((encoded >> 8) & 0xFF);
                    key = (key - 1) & 0xFFFF;
                }
            }

            File.WriteAllBytes(mapTPath, data);
        }

        public static byte[] ReadLight(string mapLPath)
        {
            var data = ReadCells8(mapLPath);
            var light = new byte[Width * Height];
            for (var x = 0; x < Width; x++)
            {
                for (var y = 0; y < Height; y++)
                {
                    var i = x * Height + y;
                    light[i] = (byte)(data[i] ^ x);
                }
            }

            return light;
        }

        public static void WriteLight(byte[] light, string mapLPath)
        {
            ArgumentNullException.ThrowIfNull(light);
            if (light.Length != Width * Height)
            {
                throw new ArgumentException($"Light array must contain {Width * Height} cells; got {light.Length}.", nameof(light));
            }

            var data = new byte[Width * Height];
            for (var x = 0; x < Width; x++)
            {
                for (var y = 0; y < Height; y++)
                {
                    var i = x * Height + y;
                    data[i] = (byte)(light[i] ^ x);
                }
            }

            File.WriteAllBytes(mapLPath, data);
        }

        public static MapObject[] ReadObjects(string mapCtPath, string mapCxPath, string mapCyPath)
        {
            var ct = File.ReadAllBytes(mapCtPath);
            var cx = File.ReadAllBytes(mapCxPath);
            var cy = File.ReadAllBytes(mapCyPath);
            if (ct.Length < ObjectSlotCount)
            {
                throw new InvalidDataException($"MAPCT '{mapCtPath}' is {ct.Length} bytes; expected at least {ObjectSlotCount}.");
            }
            if (cx.Length < ObjectSlotCount * 2)
            {
                throw new InvalidDataException($"MAPCX '{mapCxPath}' is {cx.Length} bytes; expected at least {ObjectSlotCount * 2}.");
            }
            if (cy.Length < ObjectSlotCount * 2)
            {
                throw new InvalidDataException($"MAPCY '{mapCyPath}' is {cy.Length} bytes; expected at least {ObjectSlotCount * 2}.");
            }

            var objects = new MapObject[ObjectSlotCount];
            for (var i = 0; i < ObjectSlotCount; i++)
            {
                objects[i] = new MapObject
                {
                    Type = ct[i],
                    X = (short)(cx[i * 2] | (cx[i * 2 + 1] << 8)),
                    Y = (short)(cy[i * 2] | (cy[i * 2 + 1] << 8)),
                };
            }

            return objects;
        }

        public static void WriteObjects(MapObject[] objects, string mapCtPath, string mapCxPath, string mapCyPath)
        {
            ArgumentNullException.ThrowIfNull(objects);
            if (objects.Length != ObjectSlotCount)
            {
                throw new ArgumentException($"Object array must contain {ObjectSlotCount} slots; got {objects.Length}.", nameof(objects));
            }

            var ct = new byte[ObjectSlotCount];
            var cx = new byte[ObjectSlotCount * 2];
            var cy = new byte[ObjectSlotCount * 2];
            for (var i = 0; i < ObjectSlotCount; i++)
            {
                ct[i] = objects[i].Type;
                var x = (ushort)objects[i].X;
                var y = (ushort)objects[i].Y;
                cx[i * 2] = (byte)(x & 0xFF);
                cx[i * 2 + 1] = (byte)((x >> 8) & 0xFF);
                cy[i * 2] = (byte)(y & 0xFF);
                cy[i * 2 + 1] = (byte)((y >> 8) & 0xFF);
            }

            File.WriteAllBytes(mapCtPath, ct);
            File.WriteAllBytes(mapCxPath, cx);
            File.WriteAllBytes(mapCyPath, cy);
        }

        // Reads a PAL?.COL palette and scales the 6-bit DOS components up to 8-bit
        public static List<(int r, int g, int b)> LoadPalette(string colPath)
        {
            var col = new ColProcessor().Read(colPath);
            static int Scale(int v) => (v << 2) | (v >> 4);
            return col.Select(c => (Scale(c.r), Scale(c.g), Scale(c.b))).ToList();
        }

        private static void ValidateMapArrays(MapData map)
        {
            if (map.Terrain == null || map.Terrain.Length != Width * Height)
            {
                throw new ArgumentException($"Terrain array must contain {Width * Height} cells.", nameof(map));
            }
            if (map.Overlay == null || map.Overlay.Length != Width * Height)
            {
                throw new ArgumentException($"Overlay array must contain {Width * Height} cells.", nameof(map));
            }
            if (map.Heightmap == null || map.Heightmap.Length != Width * Height)
            {
                throw new ArgumentException($"Heightmap array must contain {Width * Height} cells.", nameof(map));
            }
            if (map.Light == null || map.Light.Length != Width * Height)
            {
                throw new ArgumentException($"Light array must contain {Width * Height} cells.", nameof(map));
            }
            if (map.Objects == null || map.Objects.Length != ObjectSlotCount)
            {
                throw new ArgumentException($"Object array must contain {ObjectSlotCount} slots.", nameof(map));
            }
        }

        private static void WriteCells16(int[] cells, string path)
        {
            ArgumentNullException.ThrowIfNull(cells);
            if (cells.Length != Width * Height)
            {
                throw new ArgumentException($"Cell array must contain {Width * Height} cells; got {cells.Length}.", nameof(cells));
            }

            var data = new byte[Width * Height * 2];
            for (var i = 0; i < cells.Length; i++)
            {
                data[i * 2] = (byte)(cells[i] & 0xFF);
                data[i * 2 + 1] = (byte)((cells[i] >> 8) & 0xFF);
            }

            File.WriteAllBytes(path, data);
        }

        private static void WriteCells8(byte[] cells, string path)
        {
            ArgumentNullException.ThrowIfNull(cells);
            if (cells.Length != Width * Height)
            {
                throw new ArgumentException($"Cell array must contain {Width * Height} cells; got {cells.Length}.", nameof(cells));
            }

            File.WriteAllBytes(path, cells);
        }

        private static List<Image<Rgba32>> LoadTiles(IEnumerable<string> bimPaths, IReadOnlyList<(int r, int g, int b)> palette)
        {
            var bim = new BimProcessor { Palette = palette.ToList() };
            var tiles = new List<Image<Rgba32>>();
            foreach (var path in bimPaths)
            {
                tiles.AddRange(bim.Read(path));
            }

            if (tiles.Count == 0)
            {
                throw new InvalidDataException("No tile frames were read from the supplied BIM file(s).");
            }

            return tiles;
        }

        private static int[] ReadCells16(string path)
        {
            var data = File.ReadAllBytes(path);
            var expected = Width * Height * 2;
            if (data.Length < expected)
            {
                throw new InvalidDataException($"Map layer '{path}' is {data.Length} bytes; expected at least {expected}.");
            }

            var cells = new int[Width * Height];
            for (var i = 0; i < cells.Length; i++)
            {
                cells[i] = data[i * 2] | (data[i * 2 + 1] << 8);
            }

            return cells;
        }

        private static byte[] ReadCells8(string path)
        {
            var data = File.ReadAllBytes(path);
            var expected = Width * Height;
            if (data.Length < expected)
            {
                throw new InvalidDataException($"Map layer '{path}' is {data.Length} bytes; expected at least {expected}.");
            }

            return data;
        }

        private static void BlitTile(Image<Rgba32> image, Image<Rgba32> tile, int dx, int dy)
        {
            var w = Math.Min(TileSize, tile.Width);
            var h = Math.Min(TileSize, tile.Height);
            for (var y = 0; y < h; y++)
            {
                for (var x = 0; x < w; x++)
                {
                    var px = tile[x, y];
                    image[dx + x, dy + y] = new Rgba32(px.R, px.G, px.B, 255);
                }
            }
        }

        private static void DrawOverlayMarkers(Image<Rgba32> image, string mapOvlPath)
        {
            var overlay = ReadCells16(mapOvlPath);
            for (var ty = 0; ty < Height; ty++)
            {
                for (var tx = 0; tx < Width; tx++)
                {
                    var type = overlay[tx * Height + ty] & 0xFF;
                    if (type == 0)
                    {
                        continue;
                    }

                    DrawMarker(image, tx * TileSize + TileSize / 2, ty * TileSize + TileSize / 2, ObjectColour(type + 64));
                }
            }
        }

        private static void DrawObjects(Image<Rgba32> image, string mapCtPath, string mapCxPath, string mapCyPath)
        {
            var ct = File.ReadAllBytes(mapCtPath);
            var cx = File.ReadAllBytes(mapCxPath);
            var cy = File.ReadAllBytes(mapCyPath);
            var count = Math.Min(ct.Length, Math.Min(cx.Length, cy.Length) / 2);

            for (var i = 0; i < count; i++)
            {
                var type = ct[i];
                if (type == 0)
                {
                    continue;
                }

                var ox = cx[i * 2] | (cx[i * 2 + 1] << 8);
                var oy = cy[i * 2] | (cy[i * 2 + 1] << 8);
                if (ox < 0 || ox >= Width || oy < 0 || oy >= Height)
                {
                    continue;
                }

                var colour = ObjectColour(type);
                DrawMarker(image, ox * TileSize + TileSize / 2, oy * TileSize + TileSize / 2, colour);
            }
        }

        private static Rgba32 ObjectColour(int type)
        {
            var r = (byte)(80 + (type * 53) % 176);
            var g = (byte)(80 + (type * 101) % 176);
            var b = (byte)(80 + (type * 197) % 176);
            return new Rgba32(r, g, b, 255);
        }

        private static void DrawMarker(Image<Rgba32> image, int cx, int cy, Rgba32 colour)
        {
            const int radius = 7;
            for (var y = -radius; y <= radius; y++)
            {
                for (var x = -radius; x <= radius; x++)
                {
                    var px = cx + x;
                    var py = cy + y;
                    if (px < 0 || py < 0 || px >= image.Width || py >= image.Height)
                    {
                        continue;
                    }

                    var border = Math.Abs(x) >= radius - 1 || Math.Abs(y) >= radius - 1;
                    image[px, py] = border ? new Rgba32(0, 0, 0, 255) : colour;
                }
            }
        }
    }
}