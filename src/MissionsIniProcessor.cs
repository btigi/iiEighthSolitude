namespace ii.EighthSolitude
{
    public enum HowToWinMode
    {
        Credits = 0,
        DestroyVehicles = 1,
        DestroyBuildings = 2,
        DestroyVehiclesAndBuildings = 3,
        Repair = 4,
        Experience = 5,
        TriggerOnly = 999,
    }

    public enum MissionTriggerType
    {
        None = 0,
        Rescue = 1,
        Unused = 2,
        Arrive = 3,
        StartCard = 4,
        StartBomb = 5,
    }

    public enum MissionTriggerEffect
    {
        None = 0,
        AddUnits = 4,
        Win = 7,
    }

    public enum MissionAddUnitsPreset
    {
        MechsA = 0,
        MechsB = 1,
        VehiclesAndInfantryA = 2,
        VehiclesAndInfantryB = 3,
    }

    public enum MissionPsiCard
    {
        OneInAMillion = 0,
        BattlePsychosis = 1,
        MachineCurse = 2,
        Immolation = 3,
        GodHammer = 4,
        Equilibrium = 5,
        BattleRage = 6,
        Chaos = 7,
        Domination = 8,
        DisplacementWarp = 9,
        DoomFist = 10,
        SkillStrip = 11,
        SkillSteal = 12,
        DamageTransfer = 13,
        LifeSyphon = 14,
        Deception = 15,
        Infiltrate = 16,
        SurveillanceJam = 17,
        SummonDarkness = 18,
        SummonBlizzard = 19,
        HolyBlessing = 20,
        SummonApparition = 21,
        SummonDarkLegion = 22,
        Armageddon = 23,
        MedKit = 24,
    }

    public readonly struct MissionArriveRect : IEquatable<MissionArriveRect>
    {
        public MissionArriveRect(int x1, int y1, int x2, int y2)
        {
            X1 = x1;
            Y1 = y1;
            X2 = x2;
            Y2 = y2;
        }

        public int X1 { get; }
        public int Y1 { get; }
        public int X2 { get; }
        public int Y2 { get; }

        public int Pack()
        {
            ValidateByte(X1, nameof(X1));
            ValidateByte(Y1, nameof(Y1));
            ValidateByte(X2, nameof(X2));
            ValidateByte(Y2, nameof(Y2));
            return (X1 << 24) | (Y1 << 16) | (X2 << 8) | Y2;
        }

        public static MissionArriveRect Unpack(int value)
        {
            return new MissionArriveRect(
                (value >> 24) & 0xFF,
                (value >> 16) & 0xFF,
                (value >> 8) & 0xFF,
                value & 0xFF);
        }

        public bool Equals(MissionArriveRect other) => X1 == other.X1 && Y1 == other.Y1 && X2 == other.X2 && Y2 == other.Y2;

        public override bool Equals(object? obj) => obj is MissionArriveRect other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(X1, Y1, X2, Y2);

        public override string ToString() => $"[{X1},{Y1}]-[{X2},{Y2}]";

        public static bool operator ==(MissionArriveRect left, MissionArriveRect right) => left.Equals(right);

        public static bool operator !=(MissionArriveRect left, MissionArriveRect right) => !left.Equals(right);

        private static void ValidateByte(int value, string name)
        {
            if (value is < 0 or > 255)
            {
                throw new ArgumentOutOfRangeException(name, value, "Arrive rect components must be 0–255.");
            }
        }
    }

    public static class MissionScripting
    {
        public const int MaxTriggerSlots = 3;

        public const int RescueOwnerId = 99;

        public static IReadOnlyList<StuffDatTypeId> GetAddUnitsTypes(MissionAddUnitsPreset preset) =>
            preset switch
            {
                MissionAddUnitsPreset.MechsA => AddUnitsMechsA,
                MissionAddUnitsPreset.MechsB => AddUnitsMechsB,
                MissionAddUnitsPreset.VehiclesAndInfantryA => AddUnitsVehiclesA,
                MissionAddUnitsPreset.VehiclesAndInfantryB => AddUnitsVehiclesB,
                _ => throw new ArgumentOutOfRangeException(nameof(preset), preset, null),
            };

        public static bool IsAutomaticWinMode(HowToWinMode mode) => mode is >= HowToWinMode.Credits and <= HowToWinMode.Experience;

        public static bool IsTriggerOnlyWinMode(int howToWin) => howToWin > (int)HowToWinMode.Experience;

        private static readonly StuffDatTypeId[] AddUnitsMechsA =
        [
            StuffDatTypeId.Dominator,
            StuffDatTypeId.Obliterator,
            StuffDatTypeId.Redeemer,
            StuffDatTypeId.Obliterator,
            StuffDatTypeId.Nova,
            StuffDatTypeId.Dominator,
        ];

        private static readonly StuffDatTypeId[] AddUnitsMechsB =
        [
            StuffDatTypeId.Dominator,
            StuffDatTypeId.Obliterator,
            StuffDatTypeId.Redeemer,
            StuffDatTypeId.Obliterator,
            StuffDatTypeId.VenomTyphoon,
            StuffDatTypeId.Dominator,
        ];

        private static readonly StuffDatTypeId[] AddUnitsVehiclesA =
        [
            StuffDatTypeId.Tormentor,
            StuffDatTypeId.Tormentor,
            StuffDatTypeId.FaithHammer,
            StuffDatTypeId.FaithHammer,
            StuffDatTypeId.MachineGunner,
            StuffDatTypeId.MachineGunner,
        ];

        private static readonly StuffDatTypeId[] AddUnitsVehiclesB =
        [
            StuffDatTypeId.Avenger,
            StuffDatTypeId.Avenger,
            StuffDatTypeId.Annihilator,
            StuffDatTypeId.Annihilator,
            StuffDatTypeId.MachineGunner,
            StuffDatTypeId.MachineGunner,
        ];
    }

    public class MissionStartLocation
    {
        public int Index { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public bool IsMultiplayer { get; set; }
    }

    public class MissionTrigger
    {
        public int Index { get; set; }
        public int? Type { get; set; }
        public int? Val { get; set; }
        public int? Effect { get; set; }
        public int? Data { get; set; }

        public int? Time { get; set; }

        public MissionTriggerType? TypeKind
        {
            get => Type.HasValue ? (MissionTriggerType)Type.Value : null;
            set => Type = value.HasValue ? (int)value.Value : null;
        }

        public MissionTriggerEffect? EffectKind
        {
            get => Effect.HasValue ? (MissionTriggerEffect)Effect.Value : null;
            set => Effect = value.HasValue ? (int)value.Value : null;
        }

        public MissionAddUnitsPreset? AddUnitsPreset
        {
            get => Data.HasValue ? (MissionAddUnitsPreset)Data.Value : null;
            set => Data = value.HasValue ? (int)value.Value : null;
        }

        public MissionPsiCard? StartCard
        {
            get => Data.HasValue ? (MissionPsiCard)Data.Value : null;
            set => Data = value.HasValue ? (int)value.Value : null;
        }

        public MissionArriveRect? ArriveRect
        {
            get => Val.HasValue ? MissionArriveRect.Unpack(Val.Value) : null;
            set => Val = value?.Pack();
        }
    }

    public class Mission
    {
        public string SectionName { get; set; } = string.Empty;
        public int? Map { get; set; }
        public int? Difficulty { get; set; }
        public int? Terrain { get; set; }
        public int? StartCash { get; set; }
        public int? AIXtraCash { get; set; }
        public int? AICheatCount { get; set; }
        public string? Brief1 { get; set; }
        public string? Brief2 { get; set; }
        public string? Brief3 { get; set; }
        public string? Brief4 { get; set; }
        public int? HowToWin { get; set; }


        public int? HowToWinData1 { get; set; }
        public int? HowToWinData2 { get; set; }
        public int? HowToWinData3 { get; set; }
        public int? HowToWinData4 { get; set; }

        public int? NumAIs { get; set; }
        public int? TechLevel { get; set; }
        public string? PVStart { get; set; }
        public string? PAStart { get; set; }
        public int? RuinType { get; set; }
        public int? RuinX { get; set; }
        public int? RuinY { get; set; }
        public List<MissionStartLocation> StartLocations { get; set; } = [];
        public List<MissionTrigger> Triggers { get; set; } = [];
        public Dictionary<string, string> ExtraKeys { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        public HowToWinMode? HowToWinMode
        {
            get => HowToWin.HasValue ? (HowToWinMode)HowToWin.Value : null;
            set => HowToWin = value.HasValue ? (int)value.Value : null;
        }

        public MissionTrigger GetOrAddTrigger(int index)
        {
            if (index is < 1 or > MissionScripting.MaxTriggerSlots)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, $"Trigger index must be 1–{MissionScripting.MaxTriggerSlots}.");
            }

            var trigger = Triggers.FirstOrDefault(t => t.Index == index);
            if (trigger == null)
            {
                trigger = new MissionTrigger { Index = index };
                Triggers.Add(trigger);
            }

            return trigger;
        }
    }

    public class MissionsIniDocument
    {
        public List<string> PreambleLines { get; set; } = [];
        public List<MissionSection> Sections { get; set; } = [];

        public IEnumerable<Mission> Missions => Sections.Select(s => s.Mission);
    }

    public class MissionSection
    {
        public string HeaderLine { get; set; } = string.Empty;
        public string SectionName { get; set; } = string.Empty;
        public List<MissionLine> Lines { get; set; } = [];
        public Mission Mission { get; set; } = new();
    }

    public class MissionLine
    {
        public string Raw { get; set; } = string.Empty;
        public bool IsBlank { get; set; }
        public bool IsComment { get; set; }
        public string? Key { get; set; }
        public string? Value { get; set; }
        public string? TrailingComment { get; set; }
        public string? WhitespaceBeforeEquals { get; set; }
        public string? WhitespaceAfterEquals { get; set; }
    }

    public class MissionsIniProcessor
    {
        public MissionsIniDocument Read(string filename)
        {
            ArgumentNullException.ThrowIfNull(filename);
            return Parse(File.ReadAllLines(filename));
        }

        public MissionsIniDocument Parse(IEnumerable<string> lines)
        {
            ArgumentNullException.ThrowIfNull(lines);

            var document = new MissionsIniDocument();
            MissionSection? current = null;

            foreach (var raw in lines)
            {
                var trimmedStart = raw.TrimStart();
                if (trimmedStart.StartsWith('[') && trimmedStart.Contains(']'))
                {
                    current = new MissionSection
                    {
                        HeaderLine = raw,
                        SectionName = ExtractSectionName(trimmedStart),
                    };
                    current.Mission.SectionName = current.SectionName;
                    document.Sections.Add(current);
                    continue;
                }

                if (current == null)
                {
                    document.PreambleLines.Add(raw);
                    continue;
                }

                var line = ParseLine(raw);
                current.Lines.Add(line);
                ApplyKey(current.Mission, line);
            }

            return document;
        }

        public void Write(MissionsIniDocument document, string filename)
        {
            ArgumentNullException.ThrowIfNull(document);
            ArgumentNullException.ThrowIfNull(filename);

            SyncMissionsToLines(document);
            var lines = new List<string>();
            lines.AddRange(document.PreambleLines);
            foreach (var section in document.Sections)
            {
                lines.Add(section.HeaderLine);
                foreach (var line in section.Lines)
                {
                    lines.Add(FormatLine(line));
                }
            }

            var content = lines.Count == 0 ? string.Empty : string.Join("\r\n", lines) + "\r\n";
            File.WriteAllText(filename, content);
        }

        public List<Mission> ReadMissions(string filename)
        {
            return Read(filename).Missions.ToList();
        }

        private static string ExtractSectionName(string trimmedStart)
        {
            var open = trimmedStart.IndexOf('[');
            var close = trimmedStart.IndexOf(']');
            return trimmedStart[(open + 1)..close].Trim();
        }

        private static MissionLine ParseLine(string raw)
        {
            var trimmed = raw.Trim();
            if (trimmed.Length == 0)
            {
                return new MissionLine { Raw = raw, IsBlank = true };
            }

            if (trimmed.StartsWith(';'))
            {
                return new MissionLine { Raw = raw, IsComment = true };
            }

            var equals = raw.IndexOf('=');
            if (equals < 0)
            {
                return new MissionLine { Raw = raw, IsComment = true };
            }

            var keyPart = raw[..equals];
            var valuePart = raw[(equals + 1)..];
            var commentIndex = FindTrailingComment(valuePart);
            string? trailing = null;
            if (commentIndex >= 0)
            {
                trailing = valuePart[commentIndex..];
                valuePart = valuePart[..commentIndex];
            }

            var key = keyPart.Trim();
            var leadingWs = keyPart.Length - keyPart.TrimStart().Length;
            var trailingWs = keyPart.Length - key.Length - leadingWs;
            var valueLeadingWs = valuePart.Length - valuePart.TrimStart().Length;
            var value = valuePart.Trim();

            return new MissionLine
            {
                Raw = raw,
                Key = key,
                Value = value,
                TrailingComment = trailing,
                WhitespaceBeforeEquals = keyPart[key.Length..(key.Length + trailingWs)],
                WhitespaceAfterEquals = valuePart[..valueLeadingWs],
            };
        }

        private static int FindTrailingComment(string valuePart)
        {
            for (var i = 0; i < valuePart.Length; i++)
            {
                if (valuePart[i] != ';')
                {
                    continue;
                }

                if (i == 0 || char.IsWhiteSpace(valuePart[i - 1]))
                {
                    var start = i;
                    while (start > 0 && char.IsWhiteSpace(valuePart[start - 1]))
                    {
                        start--;
                    }

                    return start;
                }
            }

            return -1;
        }

        private static string FormatLine(MissionLine line)
        {
            if (line.IsBlank || line.IsComment || line.Key == null)
            {
                return line.Raw;
            }

            var before = line.WhitespaceBeforeEquals ?? string.Empty;
            var after = line.WhitespaceAfterEquals ?? string.Empty;
            var comment = line.TrailingComment ?? string.Empty;
            return $"{line.Key}{before}={after}{line.Value}{comment}";
        }

        private static void ApplyKey(Mission mission, MissionLine line)
        {
            if (line.Key == null || line.Value == null)
            {
                return;
            }

            var key = line.Key;
            var value = line.Value;

            switch (key.ToLowerInvariant())
            {
                case "map":
                    mission.Map = ParseInt(value);
                    break;
                case "difficulty":
                    mission.Difficulty = ParseInt(value);
                    break;
                case "terrain":
                    mission.Terrain = ParseInt(value);
                    break;
                case "startcash":
                    mission.StartCash = ParseInt(value);
                    break;
                case "aixtracash":
                    mission.AIXtraCash = ParseInt(value);
                    break;
                case "aicheatcount":
                    mission.AICheatCount = ParseInt(value);
                    break;
                case "brief1":
                    mission.Brief1 = value;
                    break;
                case "brief2":
                    mission.Brief2 = value;
                    break;
                case "brief3":
                    mission.Brief3 = value;
                    break;
                case "brief4":
                    mission.Brief4 = value;
                    break;
                case "howtowin":
                    mission.HowToWin = ParseInt(value);
                    break;
                case "howtowindata1":
                    mission.HowToWinData1 = ParseInt(value);
                    break;
                case "howtowindata2":
                    mission.HowToWinData2 = ParseInt(value);
                    break;
                case "howtowindata3":
                    mission.HowToWinData3 = ParseInt(value);
                    break;
                case "howtowindata4":
                    mission.HowToWinData4 = ParseInt(value);
                    break;
                case "numais":
                    mission.NumAIs = ParseInt(value);
                    break;
                case "techlevel":
                    mission.TechLevel = ParseInt(value);
                    break;
                case "pvstart":
                    mission.PVStart = value;
                    break;
                case "pastart":
                    mission.PAStart = value;
                    break;
                case "ruintype":
                    mission.RuinType = ParseInt(value);
                    break;
                case "ruinx":
                    mission.RuinX = ParseInt(value);
                    break;
                case "ruiny":
                    mission.RuinY = ParseInt(value);
                    break;
                default:
                    if (TryParseStartLocation(key, value, mission))
                    {
                        break;
                    }
                    if (TryParseTrigger(key, value, mission))
                    {
                        break;
                    }
                    mission.ExtraKeys[key] = value;
                    break;
            }
        }

        private static bool TryParseStartLocation(string key, string value, Mission mission)
        {
            var lower = key.ToLowerInvariant();
            var isMulti = lower.StartsWith("mstart");
            var prefix = isMulti ? "mstart" : "start";
            if (!lower.StartsWith(prefix))
            {
                return false;
            }

            var rest = lower[prefix.Length..];
            if (rest.Length < 2)
            {
                return false;
            }

            var axis = rest[0];
            if (axis is not ('x' or 'y'))
            {
                return false;
            }

            if (!int.TryParse(rest[1..], out var index))
            {
                return false;
            }

            var loc = mission.StartLocations.FirstOrDefault(s => s.Index == index && s.IsMultiplayer == isMulti);
            if (loc == null)
            {
                loc = new MissionStartLocation { Index = index, IsMultiplayer = isMulti };
                mission.StartLocations.Add(loc);
            }

            var parsed = ParseInt(value) ?? 0;
            if (axis == 'x')
            {
                loc.X = parsed;
            }
            else
            {
                loc.Y = parsed;
            }

            return true;
        }

        private static bool TryParseTrigger(string key, string value, Mission mission)
        {
            var lower = key.ToLowerInvariant();
            if (!lower.StartsWith("trigger"))
            {
                return false;
            }

            string field;
            string indexPart;
            if (lower.StartsWith("triggertype"))
            {
                field = "type";
                indexPart = lower["triggertype".Length..];
            }
            else if (lower.StartsWith("triggerval"))
            {
                field = "val";
                indexPart = lower["triggerval".Length..];
            }
            else if (lower.StartsWith("triggereffect"))
            {
                field = "effect";
                indexPart = lower["triggereffect".Length..];
            }
            else if (lower.StartsWith("triggerdata"))
            {
                field = "data";
                indexPart = lower["triggerdata".Length..];
            }
            else if (lower.StartsWith("triggertime"))
            {
                field = "time";
                indexPart = lower["triggertime".Length..];
            }
            else
            {
                return false;
            }

            if (!int.TryParse(indexPart, out var index))
            {
                return false;
            }

            var trigger = mission.Triggers.FirstOrDefault(t => t.Index == index);
            if (trigger == null)
            {
                trigger = new MissionTrigger { Index = index };
                mission.Triggers.Add(trigger);
            }

            var parsed = ParseInt(value);
            switch (field)
            {
                case "type":
                    trigger.Type = parsed;
                    break;
                case "val":
                    trigger.Val = parsed;
                    break;
                case "effect":
                    trigger.Effect = parsed;
                    break;
                case "data":
                    trigger.Data = parsed;
                    break;
                case "time":
                    trigger.Time = parsed;
                    break;
            }

            return true;
        }

        private static void SyncMissionsToLines(MissionsIniDocument document)
        {
            foreach (var section in document.Sections)
            {
                var mission = section.Mission;
                SetOrAdd(section, "Map", mission.Map?.ToString());
                SetOrAdd(section, "Difficulty", mission.Difficulty?.ToString());
                SetOrAdd(section, "Terrain", mission.Terrain?.ToString());
                SetOrAdd(section, "StartCash", mission.StartCash?.ToString());
                SetOrAdd(section, "AIXtraCash", mission.AIXtraCash?.ToString());
                SetOrAdd(section, "AICheatCount", mission.AICheatCount?.ToString());
                SetOrAdd(section, "Brief1", mission.Brief1);
                SetOrAdd(section, "Brief2", mission.Brief2);
                SetOrAdd(section, "Brief3", mission.Brief3);
                SetOrAdd(section, "Brief4", mission.Brief4);
                SetOrAdd(section, "HowToWin", mission.HowToWin?.ToString());
                SetOrAdd(section, "HowToWinData1", mission.HowToWinData1?.ToString());
                SetOrAdd(section, "HowToWinData2", mission.HowToWinData2?.ToString());
                SetOrAdd(section, "HowToWinData3", mission.HowToWinData3?.ToString());
                SetOrAdd(section, "HowToWinData4", mission.HowToWinData4?.ToString());
                SetOrAdd(section, "NumAIs", mission.NumAIs?.ToString());
                SetOrAdd(section, "TechLevel", mission.TechLevel?.ToString());
                SetOrAdd(section, "PVStart", mission.PVStart);
                SetOrAdd(section, "PAStart", mission.PAStart);
                SetOrAdd(section, "RuinType", mission.RuinType?.ToString());
                SetOrAdd(section, "RuinX", mission.RuinX?.ToString());
                SetOrAdd(section, "RuinY", mission.RuinY?.ToString());

                foreach (var loc in mission.StartLocations.OrderBy(l => l.IsMultiplayer).ThenBy(l => l.Index))
                {
                    var prefix = loc.IsMultiplayer ? "MStart" : "Start";
                    SetOrAdd(section, $"{prefix}X{loc.Index}", loc.X.ToString());
                    SetOrAdd(section, $"{prefix}Y{loc.Index}", loc.Y.ToString());
                }

                foreach (var trigger in mission.Triggers.OrderBy(t => t.Index))
                {
                    if (trigger.Type.HasValue)
                    {
                        SetOrAdd(section, $"TriggerType{trigger.Index}", trigger.Type.Value.ToString());
                    }
                    if (trigger.Val.HasValue)
                    {
                        SetOrAdd(section, $"TriggerVal{trigger.Index}", trigger.Val.Value.ToString());
                    }
                    if (trigger.Effect.HasValue)
                    {
                        SetOrAdd(section, $"TriggerEffect{trigger.Index}", trigger.Effect.Value.ToString());
                    }
                    if (trigger.Data.HasValue)
                    {
                        SetOrAdd(section, $"TriggerData{trigger.Index}", trigger.Data.Value.ToString());
                    }
                    if (trigger.Time.HasValue)
                    {
                        SetOrAdd(section, $"TriggerTime{trigger.Index}", trigger.Time.Value.ToString());
                    }
                }

                foreach (var pair in mission.ExtraKeys)
                {
                    SetOrAdd(section, pair.Key, pair.Value);
                }
            }
        }

        private static void SetOrAdd(MissionSection section, string key, string? value)
        {
            if (value == null)
            {
                return;
            }

            var existing = section.Lines.FirstOrDefault(l =>
                l.Key != null && string.Equals(l.Key, key, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                existing.Value = value;
                return;
            }

            section.Lines.Add(new MissionLine
            {
                Key = key,
                Value = value,
                WhitespaceBeforeEquals = string.Empty,
                WhitespaceAfterEquals = string.Empty,
            });
        }

        private static int? ParseInt(string value)
        {
            if (int.TryParse(value.Trim(), out var result))
            {
                return result;
            }

            return null;
        }
    }
}
