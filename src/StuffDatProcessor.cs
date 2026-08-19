namespace ii.EighthSolitude
{
    public enum StuffDatTypeId
    {
        OreCarrier = 0,
        OreTruck = 1,
        Marauder = 2,
        Oppressor = 3,
        Crucifier = 4,
        Apc = 5,
        Tormentor = 6,
        Avenger = 7,
        FaithHammer = 8,
        Annihilator = 9,
        Purifier = 10,
        LandMine = 11,
        Missile = 12,
        Infantry = 13,
        MachineGunner = 14,
        MachineGunnerCh = 15,
        MortarUnit = 16,
        Priest = 17,
        Medic = 18,
        SlavenRider = 19,
        Marine = 20,
        Commander = 21,
        AirTransport = 22,
        Sparrow = 23,
        Eagle = 24,
        Hovercraft = 25,
        SpySatellite = 26,
        PowerUpgrade = 27,
        LaserUpgrade = 28,
        ShellUpgrade = 29,
        RifleUpgrade = 30,
        BodyArmourUpgrade = 31,
        ArmourPlatingUpgrade = 32,
        Dominator = 33,
        Obliterator = 34,
        LightMech = 35,
        Nova = 36,
        VenomTyphoon = 37,
        Redeemer = 38,
        Bomb = 39,
        Stealth = 40,
        Trueseeing = 41,
        MobileBase = 42,
        Pyroclast = 43,

        Base = 1000,
        Foundation = 1001,
        PowerPlant = 1002,
        Mine = 1003,
        Refinery = 1004,
        Barracks = 1005,
        Wall = 1006,
        RadarStation = 1007,
        Hospital = 1008,
        VehicleFactory = 1009,
        GunEmplacement = 1010,
        HiTechLab = 1011,
        RepairBay = 1012,
        RobotHangar = 1013,
        AdvancedMine = 1014,
        Reactor = 1015,
        MissileSilo = 1016,
        ShieldGenerator = 1017,
        NavalYard = 1018,
        AirHangar = 1019,
        LandingPad = 1020,
        ChemicalPlant = 1021,
        SuperGun = 1022,
    }

    public class StuffDatEntry
    {
        public StuffDatTypeId TypeId { get; set; }
        public int Cost { get; set; }
        public int Armour { get; set; }
        public int Weapon { get; set; }
        public int Weapon2 { get; set; }
        public int Speed { get; set; }
        public int TurnSpeed { get; set; }
        public int Health { get; set; }
        public int BuildTime { get; set; }
        public int ReloadTime { get; set; }

        public bool IsBuilding => TypeId >= StuffDatTypeId.Base;
    }

    public class StuffDatWeapon
    {
        public int DamageVsBody { get; set; }
        public int DamageVsLight { get; set; }
        public int DamageVsMedium { get; set; }
        public int DamageVsHeavy { get; set; }
        public int DamageVsStructure { get; set; }
        public int[] Extra { get; set; } = new int[StuffDatProcessor.WeaponExtraDwords];
    }

    public class StuffDatProcessor
    {
        public const int Sentinel = -1;
        public const int WeaponRecordCount = 20;
        public const int WeaponDamageCount = 5;
        public const int WeaponExtraDwords = 15;
        public const int WeaponRecordDwords = WeaponDamageCount + WeaponExtraDwords;
        public const int WeaponTableBytes = WeaponRecordCount * WeaponRecordDwords * sizeof(int);

        public List<StuffDatWeapon> Weapons { get; set; } = [];

        public List<StuffDatEntry> Read(string filename)
        {
            ArgumentNullException.ThrowIfNull(filename);

            using var stream = new FileStream(filename, FileMode.Open, FileAccess.Read);
            using var reader = new BinaryReader(stream);

            var entries = new List<StuffDatEntry>();
            while (true)
            {
                EnsureBytes(reader, sizeof(int), filename);
                var typeId = reader.ReadInt32();
                if (typeId == Sentinel)
                {
                    break;
                }

                if (typeId >= (int)StuffDatTypeId.Base)
                {
                    EnsureBytes(reader, sizeof(int) * 5, filename);
                    entries.Add(new StuffDatEntry
                    {
                        TypeId = (StuffDatTypeId)typeId,
                        Cost = reader.ReadInt32(),
                        Armour = reader.ReadInt32(),
                        Weapon = reader.ReadInt32(),
                        Health = reader.ReadInt32(),
                        BuildTime = reader.ReadInt32(),
                    });
                }
                else
                {
                    EnsureBytes(reader, sizeof(int) * 9, filename);
                    entries.Add(new StuffDatEntry
                    {
                        TypeId = (StuffDatTypeId)typeId,
                        Cost = reader.ReadInt32(),
                        Armour = reader.ReadInt32(),
                        Weapon = reader.ReadInt32(),
                        Weapon2 = reader.ReadInt32(),
                        Speed = reader.ReadInt32(),
                        TurnSpeed = reader.ReadInt32(),
                        Health = reader.ReadInt32(),
                        BuildTime = reader.ReadInt32(),
                        ReloadTime = reader.ReadInt32(),
                    });
                }
            }

            Weapons = ReadWeapons(reader, filename);
            return entries;
        }

        public void Write(List<StuffDatEntry> entries, string filename)
        {
            ArgumentNullException.ThrowIfNull(entries);
            ArgumentNullException.ThrowIfNull(filename);

            using var stream = new FileStream(filename, FileMode.Create, FileAccess.Write);
            using var writer = new BinaryWriter(stream);

            foreach (var entry in entries)
            {
                if (entry == null)
                {
                    throw new ArgumentException("STUFF entry list cannot contain null entries.", nameof(entries));
                }
                if ((int)entry.TypeId == Sentinel)
                {
                    throw new ArgumentException($"STUFF type id {Sentinel} is reserved as the record terminator.", nameof(entries));
                }

                writer.Write((int)entry.TypeId);
                writer.Write(entry.Cost);
                writer.Write(entry.Armour);
                writer.Write(entry.Weapon);

                if (entry.IsBuilding)
                {
                    writer.Write(entry.Health);
                    writer.Write(entry.BuildTime);
                }
                else
                {
                    writer.Write(entry.Weapon2);
                    writer.Write(entry.Speed);
                    writer.Write(entry.TurnSpeed);
                    writer.Write(entry.Health);
                    writer.Write(entry.BuildTime);
                    writer.Write(entry.ReloadTime);
                }
            }

            writer.Write(Sentinel);
            WriteWeapons(writer);
        }

        private static List<StuffDatWeapon> ReadWeapons(BinaryReader reader, string filename)
        {
            var remaining = reader.BaseStream.Length - reader.BaseStream.Position;
            if (remaining == 0)
            {
                return [];
            }
            if (remaining != WeaponTableBytes)
            {
                throw new InvalidDataException($"Invalid STUFF weapon table size: {remaining} bytes in '{filename}'. Expected 0 or {WeaponTableBytes} bytes.");
            }

            var weapons = new List<StuffDatWeapon>(WeaponRecordCount);
            for (var i = 0; i < WeaponRecordCount; i++)
            {
                var extra = new int[WeaponExtraDwords];
                var weapon = new StuffDatWeapon
                {
                    DamageVsBody = reader.ReadInt32(),
                    DamageVsLight = reader.ReadInt32(),
                    DamageVsMedium = reader.ReadInt32(),
                    DamageVsHeavy = reader.ReadInt32(),
                    DamageVsStructure = reader.ReadInt32(),
                    Extra = extra,
                };

                for (var j = 0; j < WeaponExtraDwords; j++)
                {
                    extra[j] = reader.ReadInt32();
                }

                weapons.Add(weapon);
            }

            return weapons;
        }

        private void WriteWeapons(BinaryWriter writer)
        {
            if (Weapons == null || Weapons.Count == 0)
            {
                return;
            }

            for (var i = 0; i < WeaponRecordCount; i++)
            {
                var weapon = i < Weapons.Count ? Weapons[i] : null;
                writer.Write(weapon?.DamageVsBody ?? 0);
                writer.Write(weapon?.DamageVsLight ?? 0);
                writer.Write(weapon?.DamageVsMedium ?? 0);
                writer.Write(weapon?.DamageVsHeavy ?? 0);
                writer.Write(weapon?.DamageVsStructure ?? 0);

                var extra = weapon?.Extra;
                for (var j = 0; j < WeaponExtraDwords; j++)
                {
                    writer.Write(extra != null && j < extra.Length ? extra[j] : 0);
                }
            }
        }

        private static void EnsureBytes(BinaryReader reader, int count, string filename)
        {
            var remaining = reader.BaseStream.Length - reader.BaseStream.Position;
            if (remaining < count)
            {
                throw new InvalidDataException($"Truncated STUFF file '{filename}'.");
            }
        }
    }
}
