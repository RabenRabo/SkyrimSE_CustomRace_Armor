using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Skyrim;
using System.Text.RegularExpressions;

namespace SSE.CRA.BL
{
    public class PreProcessResult
    {
        #region fields
        public readonly Dictionary<FormKey, ArmorInfo> Armors = [];
        public readonly Dictionary<FormKey, ArmorAddonInfo> ArmorAddons = [];
        public HashSet<FormKey> Races = [];
        #endregion

        #region methods
        public ArmorGroupInfo CompileArmorGroupInfo(ArmorInfo current)
        {
            HashSet<ArmorInfo> armor = [];
            HashSet<ArmorAddonInfo> armorAddons = [];
            CompileArmorGroupInfoRecursive(current, armor, armorAddons);
            int newRecords = armorAddons.Sum(aa => aa.NewRecords);
            int overrideRecords = armorAddons.Sum(aa => aa.OverrideRecords);
            var requiredMasters = armor.Select(a => a.Key.ModKey).Union(armorAddons.Select(aa => aa.Key.ModKey)).ToHashSet();
            return new(armor, armorAddons, newRecords, overrideRecords, requiredMasters);
        }
        #endregion

        #region methods (helping)
        private void CompileArmorGroupInfoRecursive(ArmorInfo current, HashSet<ArmorInfo> armor, HashSet<ArmorAddonInfo> armorAddons)
        {
            current.AssignedToGroup = true;
            armor.Add(current);
            foreach(var aa in current.ArmorAddonKeys)
            {
                ArmorAddonInfo aaInfo = ArmorAddons[aa];
                if (armorAddons.Add(aaInfo))
                {
                    foreach(var next in aaInfo.Armors)
                    {
                        if (!armor.Contains(next))
                        {
                            CompileArmorGroupInfoRecursive(next, armor, armorAddons);
                        }
                    }
                }
            }
        }
        #endregion
    }

    public readonly struct ArmorGroupInfo(IEnumerable<ArmorInfo> armor, IEnumerable<ArmorAddonInfo> armorAddon, int newRecs, int overrideRecs, HashSet<ModKey> reqMast)
    {
        #region fields
        public readonly IEnumerable<ArmorInfo> Armor = armor;
        public readonly IEnumerable<ArmorAddonInfo> ArmorAddons = armorAddon;
        public readonly int NewRecords = newRecs;
        public readonly int OVerrideRecords = overrideRecs;
        public readonly HashSet<ModKey> RequiredMasters = reqMast;
        #endregion

        #region methods
        public void WriteTo(Modding.ArmorProcessingInfo processingInfo, SkyrimMod mod)
        {
            foreach(var aaInfo in ArmorAddons)
            {
                aaInfo.Process(processingInfo, mod);
            }
        }
        #endregion
    }

    public class ArmorInfo : IEquatable<ArmorInfo>
    {
        #region fields
        public readonly FormKey Key;
        public readonly IArmorGetter Original;
        public readonly HashSet<FormKey> ArmorAddonKeys = [];
        #endregion

        #region properties
        public bool AssignedToGroup { get; set; }
        public Armor? Override { get; set; }
        #endregion

        #region ctors
        public ArmorInfo(FormKey key, IArmorGetter orig)
        {
            Key = key;
            Original = orig;
        }
        #endregion

        #region methods
        public bool Equals(ArmorInfo? other)
        {
            return other is not null && Key == other.Key;
        }
        public override bool Equals(object? obj)
        {
            return obj is ArmorInfo other && Equals(other);
        }
        public override int GetHashCode()
        {
            return Key.GetHashCode();
        }
        #endregion
    }

    public class ArmorAddonInfo : IEquatable<ArmorAddonInfo>
    {
        #region fields
        public readonly FormKey Key;
        public readonly IArmorAddonGetter Original;
        public readonly HashSet<ArmorInfo> Armors = [];
        public readonly Dictionary<FormKey, ArmorAddonRaceInfo> Races = [];
        #endregion

        #region properties
        public int NewRecords => Races.Values.Sum(r => r.NewRecords);
        public int OverrideRecords => Races.Values.Sum(r => r.OverrideRecords);
        public ArmorAddon? Override { get; set; }
        #endregion

        #region ctors
        public ArmorAddonInfo(FormKey key, IArmorAddonGetter orig)
        {
            Key = key;
            Original = orig;
        }
        #endregion

        #region methods
        public void Process(Modding.ArmorProcessingInfo processingInfo, SkyrimMod mod)
        {
            foreach(var race in Races.Values)
            {
                race.Process(processingInfo, mod);
            }
        }
        public bool Equals(ArmorAddonInfo? other)
        {
            return other is not null && Key == other.Key;
        }
        public override bool Equals(object? obj)
        {
            return obj is ArmorAddonInfo other && Equals(other);
        }
        public override int GetHashCode()
        {
            return Key.GetHashCode();
        }
        #endregion
    }

    public abstract class ArmorAddonRaceInfo
    {
        #region fields
        public readonly ArmorAddonInfo Owner;
        public readonly Modding.RaceID Race;
        public readonly IEnumerable<Modding.RaceID> AdditionalRaces;
        #endregion

        #region properties
        public abstract int NewRecords { get; }
        public abstract int OverrideRecords { get; }
        #endregion

        #region ctors
        protected ArmorAddonRaceInfo(ArmorAddonInfo owner, Modding.RaceID race, IEnumerable<Modding.RaceID> additionalRaces)
        {
            Owner = owner;
            Race = race;
            AdditionalRaces = additionalRaces;
        }
        #endregion

        #region methods
        public abstract void Process(Modding.ArmorProcessingInfo processingInfo, SkyrimMod mod);
        #endregion
    }

    /// <summary>
    /// Creates a duplicate ArmorAddon, sets Race/AdditionalRaces and updates model paths
    /// </summary>
    public class ArmorAddonRaceNewInfo : ArmorAddonRaceInfo
    {
        #region properties
        public ModelPathRegexInfo? Male { get; set; }
        public ModelPathRegexInfo? Female { get; set; }
        #endregion

        #region properties
        public override int NewRecords => 1;
        public override int OverrideRecords => 0;
        #endregion

        #region ctors
        public ArmorAddonRaceNewInfo(ArmorAddonInfo owner, Modding.RaceID race, IEnumerable<Modding.RaceID> additionalRaces) : base(owner, race, additionalRaces) { }
        #endregion

        #region methods
        public override void Process(Modding.ArmorProcessingInfo processingInfo, SkyrimMod mod)
        {
            string newEditorID = Race.EditorID + Owner.Original.EditorID;
            ArmorAddon newAA = mod.ArmorAddons.DuplicateInAsNewRecord(Owner.Original, newEditorID, null);
            newAA.EditorID = newEditorID;
            if (Male is not null)
            {
                string oldPath = newAA.WorldModel!.Male!.File.GivenPath;
                string newPath = Male.Regex.Replace(oldPath, Male.Replacer);
                if (!File.Exists(Path.Combine(processingInfo.GameDataPath, "meshes", newPath)) && processingInfo.MissingModelPaths.Add(newPath))
                {
                    PrintMissingPath(processingInfo.Progress, newPath, oldPath);
                }
                newAA.WorldModel!.Male!.File.GivenPath = newPath;
                if (newAA.FirstPersonModel?.Male?.File.GivenPath == oldPath)
                {
                    newAA.FirstPersonModel!.Male!.File.GivenPath = newPath;
                }
            }
            // check if should replace female model path
            if (Female is not null)
            {
                string oldPath = newAA.WorldModel!.Female!.File.GivenPath;
                string newPath = Female.Regex.Replace(oldPath, Female.Replacer);
                if (!File.Exists(Path.Combine(processingInfo.GameDataPath, "meshes", newPath)) && processingInfo.MissingModelPaths.Add(newPath))
                {
                    PrintMissingPath(processingInfo.Progress, newPath, oldPath);
                }
                newAA.WorldModel!.Female!.File.GivenPath = newPath;
                if (newAA.FirstPersonModel?.Female?.File.GivenPath == oldPath)
                {
                    newAA.FirstPersonModel!.Female!.File.GivenPath = newPath;
                }
            }
            // set Race and AdditionalRaces
            newAA.Race = Race.Getter!.ToNullableLink();
            newAA.AdditionalRaces.Clear();
            foreach (var addRace in AdditionalRaces)
            {
                newAA.AdditionalRaces.Add(addRace.Key);
            }

            // add new ArmorAddon to all armors that use the original one
            foreach (var armorInfo in Owner.Armors)
            {
                if (armorInfo.Override is null)
                {
                    armorInfo.Override = mod.Armors.GetOrAddAsOverride(armorInfo.Original);
                }
                armorInfo.Override.Armature.Add(newAA);
            }
        }
        #endregion

        #region methods (helping)
        private void PrintMissingPath(IProgress<ProgressInfo>? progress, string newPath, string oldPath)
        {
            progress?.Report(new ProgressInfo(ProgressInfoTypes.Warning, $"{Owner.Original.FormKey.ModKey.Name}: meshes\\{newPath} not found in (prev. meshes\\{oldPath})"));
            bool first = true;
            ProgressInfoTypes level = ProgressInfoTypes.Debug;
            ArmorInfo? firstArm = Owner.Armors.FirstOrDefault(a => a.Key.ModKey == Owner.Key.ModKey);
            if (firstArm is not null)
            {
                progress?.Report(new ProgressInfo(level, $" --> {firstArm.Key}"));
                first = false;
                level = ProgressInfoTypes.Trace;
            }
            foreach (var arm in Owner.Armors.Where(a => !ReferenceEquals(a, firstArm)))
            {
                progress?.Report(new ProgressInfo(level, $" --> {arm.Key}"));
                if (first)
                {
                    first = false;
                    level = ProgressInfoTypes.Trace;
                }
            }
        }
        #endregion
    }

    /// <summary>
    /// Creates an ArmorAddon override and adds this race to AdditionalRaces
    /// </summary>
    public class ArmorAddonRaceExtInfo : ArmorAddonRaceInfo
    {
        #region properties
        public override int NewRecords => 0;
        public override int OverrideRecords => 1;
        #endregion

        #region ctors
        public ArmorAddonRaceExtInfo(ArmorAddonInfo owner, Modding.RaceID race, IEnumerable<Modding.RaceID> additionalRaces) : base(owner, race, additionalRaces) { }
        #endregion

        #region methods
        public override void Process(Modding.ArmorProcessingInfo processingInfo, SkyrimMod mod)
        {
            if (Owner.Override is null)
            {
                Owner.Override = mod.ArmorAddons.GetOrAddAsOverride(Owner.Original);
            }
            Owner.Override.AdditionalRaces.Add(Race.Key);
            foreach (var addRace in AdditionalRaces)
            {
                Owner.Override.AdditionalRaces.Add(addRace.Key);
            }
        }
        #endregion
    }

    public class ModelPathRegexInfo
    {
        #region fields
        public readonly Regex Regex;
        public readonly string Replacer;
        #endregion

        #region ctors
        public ModelPathRegexInfo(Regex regex, string replacer)
        {
            Regex = regex;
            Replacer = replacer;
        }
        #endregion
    }
}
