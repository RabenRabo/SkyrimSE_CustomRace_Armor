using DynamicData;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Environments;
using Mutagen.Bethesda.FormKeys.SkyrimSE;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Skyrim;
using Noggog;
using System.Text.RegularExpressions;

namespace SSE.CRA.BL
{
    public class Modding : IDisposable
    {
        #region fields
        private readonly IGameEnvironment<ISkyrimMod, ISkyrimModGetter> _environment = GameEnvironment.Typical.Skyrim(SkyrimRelease.SkyrimSE);
        #endregion

        #region methods
        public string GetGameDataPath()
        {
            return _environment.DataFolderPath.Path;
        }
        /// <summary>
        /// Returns a list of all non-vanilla races (along with their vampire equivalent)
        /// </summary>
        /// <returns></returns>
        public IEnumerable<KeyValuePair<IRaceGetter, IRaceGetter>> GetRaces()
        {
            Dictionary<string, KeyValuePair<IRaceGetter?, IRaceGetter?>> res = [];
            foreach (var raceGetter in _environment.LoadOrder.PriorityOrder.Race().WinningOverrides())
            {
                if (raceGetter.EditorID is not null && raceGetter.FormKey.ModKey != Skyrim.ModKey)
                {
                    string editorID;
                    bool isVamp;
                    if (raceGetter.EditorID.EndsWith("Vampire"))
                    {
                        editorID = raceGetter.EditorID[..^"Vampire".Length];
                        isVamp = true;
                    }
                    else
                    {
                        editorID = raceGetter.EditorID;
                        isVamp = false;
                    }
                    if (!res.TryGetValue(editorID, out var pair))
                    {
                        pair = new();
                    }
                    res[editorID] = new(isVamp ? pair.Key : raceGetter, isVamp ? raceGetter : pair.Value);
                }
            }
            return res.Values.Where(pair => pair.Key is not null && pair.Value is not null).Cast<KeyValuePair<IRaceGetter, IRaceGetter>>().ToArray();
        }
        public IRaceGetter GetRace(string editorID)
        {
            return _environment.LoadOrder.PriorityOrder.Race().WinningOverrides().First(r => r.EditorID == editorID);
        }
        public SkyrimMod CreateMod(ModKey modKey)
        {
            return new SkyrimMod(modKey, SkyrimRelease.SkyrimSE);
        }
        public RacePair RemoveArmorRace(SkyrimMod target, string editorID, string vampEditorID)
        {
            IRaceGetter race = GetRace(editorID);
            IRaceGetter vamp = GetRace(vampEditorID);
            Race ovRace = target.Races.GetOrAddAsOverride(race);
            ovRace.ArmorRace.Clear();
            Race ovVamp = target.Races.GetOrAddAsOverride(vamp);
            ovVamp.ArmorRace.Clear();
            return new(ovRace, ovVamp);
        }
        public int SetArmorRaceOfSkin(ArmorProcessingInfo processingInfo)
        {
            int count = 0;
            foreach (var raceInfo in processingInfo.Races)
            {
                IArmorGetter skinArmor = raceInfo.Race.MainRace.Skin.Resolve(_environment.LinkCache);
                foreach (IArmorAddonGetter armature in skinArmor.Armature.Select(a => a.Resolve(_environment.LinkCache)))
                {
                    if (CheckIfNakedSkinArmature(armature, raceInfo))
                    {
                        ArmorAddon aa = processingInfo.Target.ArmorAddons.GetOrAddAsOverride(armature);
                        aa.Race = raceInfo.Race.MainRace.ToNullableLink();
                        aa.AdditionalRaces.Clear();
                        aa.AdditionalRaces.Add(raceInfo.Race.VampireRace);
                        count++;
                    }
                }
            }
            return count;
        }
        public ProcessedArmorResult ProcessArmor(ArmorProcessingInfo processingInfo, IProgress<ProgressInfo>? progress, CancellationToken? cancellationToken)
        {
            var overriddenArmorAddons = new Dictionary<FormKey, KeyValuePair<ArmorAddon, HashSet<FormKey>>>();
            var newArmorAddons = new Dictionary<string, ArmorAddon>();
            int armorCount = 0;
            int armorAddonOWCount = 0;
            int newArmorAddonCount = 0;
            HashSet<string> missingPaths = [];
            foreach (var armorGetter in _environment.LoadOrder.PriorityOrder.Armor().WinningOverrides())
            {
                if (cancellationToken.HasValue && cancellationToken.Value.IsCancellationRequested) break;
                bool armorProcessed = false;
                foreach (var raceInfo in processingInfo.Races)
                {
                    if (_environment.LoadOrder.ContainsKey(armorGetter.FormKey.ModKey) && !processingInfo.GenSettings.IgnoreEditorIDs.Contains(armorGetter.EditorID!) && CheckIfWearableArmor(processingInfo, raceInfo, armorGetter))
                    {
                        Armor? a = null;
                        progress?.Report(new ProgressInfo(ProgressInfoTypes.Debug, "processing " + armorGetter.EditorID + " for " + raceInfo.Race.MainRace.EditorID));
                        foreach (var arm in armorGetter.Armature)
                        {
                            IArmorAddonGetter aa = arm.Resolve(_environment.LinkCache);
                            try
                            {
                                if (aa.Race.FormKey == Skyrim.Race.DefaultRace.FormKey || aa.AdditionalRaces.Any(ar => ar.FormKey == Skyrim.Race.DefaultRace.FormKey))
                                {
                                    if (cancellationToken.HasValue && cancellationToken.Value.IsCancellationRequested) break;
                                    armorProcessed = true;
                                    if (raceInfo.CheckIfNeedsCustomArmature(aa, out KeyValuePair<Regex, string>? maleRegex, out KeyValuePair<Regex, string>? femaleRegex))
                                    {
                                        if (a is null)
                                        {
                                            a = processingInfo.Target.Armors.GetOrAddAsOverride(armorGetter);
                                        }
                                        a.Armature.Add(CreateCustomArmature(processingInfo, raceInfo, missingPaths, newArmorAddons, a, aa, maleRegex, femaleRegex, progress));
                                        newArmorAddonCount++;
                                    }
                                    else
                                    {
                                        ExtendExistingArmature(processingInfo, raceInfo, overriddenArmorAddons, aa);
                                        armorAddonOWCount++;
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                progress?.Report(new ProgressInfo(ProgressInfoTypes.Error, ex.Message));
                            }
                        }
                    }
                }
                if (armorProcessed) armorCount++;
            }
            return new(armorCount, armorAddonOWCount, newArmorAddonCount, missingPaths.Count);
        }
        public static bool ExistsMod(string gameDataPath, ModKey modKey)
        {
            return File.Exists(Path.Combine(gameDataPath, modKey.FileName.String));
        }
        public static void DeleteMod(string gameDataPath, ModKey modKey)
        {
            File.Delete(Path.Combine(gameDataPath, modKey.FileName.String));
        }
        public void WriteMod(ArmorProcessingInfo processingInfo)
        {
            processingInfo.Target.BeginWrite
            .ToPath(Path.Combine(_environment.DataFolderPath.Path, processingInfo.Target.ModKey.FileName.String))
            .WithDefaultLoadOrder()
            .WriteAsync().Wait();
        }
        public void Dispose()
        {
            _environment.Dispose();
        }
        #endregion

        #region methods (helping)
        private bool CheckIfNakedSkinArmature(IArmorAddonGetter aa, ArmorRaceProcessingInfo raceInfo)
        {
            return aa.Race.FormKey == Skyrim.Race.DefaultRace.FormKey && aa.AdditionalRaces.Count == 2 && aa.AdditionalRaces.Contains(raceInfo.Race.MainRace.FormKey) && aa.AdditionalRaces.Contains(raceInfo.Race.VampireRace.FormKey);
        }
        private bool CheckIfWearableArmor(ArmorProcessingInfo processingInfo, ArmorRaceProcessingInfo raceInfo, IArmorGetter armorGetter)
        {
            return armorGetter.EditorID is not null && !armorGetter.EditorID.StartsWith("SkinNaked") &&
                !armorGetter.Armature.Any(aa => CheckNonWearableArmorAA(processingInfo, raceInfo, aa.Resolve(_environment.LinkCache))) && // check if any AAs marked as unwearable
                (armorGetter.Race.FormKey == Skyrim.Race.DefaultRace.FormKey || armorGetter.Armature.Any(aa => CheckIfDefaultRaceArmorAddon(aa.Resolve(_environment.LinkCache)))); // check if can be worn by default race
        }
        private bool CheckNonWearableArmorAA(ArmorProcessingInfo processingInfo, ArmorRaceProcessingInfo raceInfo, IArmorAddonGetter aa)
        {
            if (aa.WorldModel is not null)
            {
                if (raceInfo.ProcessMale && aa.WorldModel.Male is not null)
                {
                    return processingInfo.NonWearableArmorRegexes.Any(nar => nar.IsMatch(aa.WorldModel.Male.File.GivenPath));
                }
                if (raceInfo.ProcessFemale && aa.WorldModel.Female is not null)
                {
                    return processingInfo.NonWearableArmorRegexes.Any(nar => nar.IsMatch(aa.WorldModel.Female.File.GivenPath));
                }
            }
            return false;
        }
        private static bool CheckIfDefaultRaceArmorAddon(IArmorAddonGetter aa)
        {
            return (!aa.Race.IsNull && aa.Race.FormKey == Skyrim.Race.DefaultRace.FormKey) || aa.AdditionalRaces.Any(ar => ar.FormKey == Skyrim.Race.DefaultRace.FormKey);
        }
        private ArmorAddon CreateCustomArmature(ArmorProcessingInfo processingInfo, ArmorRaceProcessingInfo raceInfo, HashSet<string> missingPaths, Dictionary<string, ArmorAddon> newArmorAddons, Armor armor, IArmorAddonGetter armature, KeyValuePair<Regex, string>? maleRegex, KeyValuePair<Regex, string>? femaleRegex, IProgress<ProgressInfo>? progress)
        {
            progress?.Report(new ProgressInfo(ProgressInfoTypes.Trace, "CreateCustomArmature()"));
            string newEditorID = raceInfo.Race.MainRace.EditorID + armature.EditorID;
            // check if new AA not created yet
            if (!newArmorAddons.TryGetValue(newEditorID, out var aa))
            {
                progress?.Report(new ProgressInfo(ProgressInfoTypes.Debug, "creating new custom armature"));
                aa = processingInfo.Target.ArmorAddons.DuplicateInAsNewRecord(armature, newEditorID, null);
                aa.EditorID = newEditorID;
                newArmorAddons.Add(newEditorID, aa);
                // check if should replace male model path
                if (maleRegex is not null)
                {
                    string oldPath = aa.WorldModel!.Male!.File.GivenPath;
                    string newPath = maleRegex.Value.Key.Replace(oldPath, maleRegex.Value.Value);
                    if (!File.Exists(Path.Combine(_environment.DataFolderPath.Path, "meshes", newPath)) && missingPaths.Add(newPath))
                    {
                        progress?.Report(new ProgressInfo(ProgressInfoTypes.Warning, $"{newPath} ({armor.FormKey}, {armor.EditorID}) not found in meshes folder (prev. {oldPath})"));
                    }
                    aa.WorldModel!.Male!.File.GivenPath = newPath;
                    if (aa.FirstPersonModel?.Male?.File.GivenPath == oldPath)
                    {
                        aa.FirstPersonModel!.Male!.File.GivenPath = newPath;
                    }
                }
                // check if should replace female model path
                if (femaleRegex is not null)
                {
                    string oldPath = aa.WorldModel!.Female!.File.GivenPath;
                    string newPath = femaleRegex.Value.Key.Replace(oldPath, femaleRegex.Value.Value);
                    if (!File.Exists(Path.Combine(_environment.DataFolderPath.Path, "meshes", newPath)) && missingPaths.Add(newPath))
                    {
                        progress?.Report(new ProgressInfo(ProgressInfoTypes.Warning, $"{newPath} ({armor.FormKey}, {armor.EditorID}) not found in meshes folder (prev. {oldPath})"));
                    }
                    aa.WorldModel!.Female!.File.GivenPath = newPath;
                    if (aa.FirstPersonModel?.Female?.File.GivenPath == oldPath)
                    {
                        aa.FirstPersonModel!.Female!.File.GivenPath = newPath;
                    }
                }
                // set Race and AdditionalRaces
                aa.Race = raceInfo.Race.MainRace.ToNullableLink();
                aa.AdditionalRaces.Clear();
                aa.AdditionalRaces.Add(raceInfo.Race.VampireRace.ToNullableLink());
            }
            else
            {
                progress?.Report(new ProgressInfo(ProgressInfoTypes.Trace, "custom armature already exists"));
            }
            return aa;
        }
        private void ExtendExistingArmature(ArmorProcessingInfo processingInfo, ArmorRaceProcessingInfo raceInfo, Dictionary<FormKey, KeyValuePair<ArmorAddon, HashSet<FormKey>>> overriddenArmorAddons, IArmorAddonGetter armature)
        {
            // check if AA not overridden yet
            if (!overriddenArmorAddons.TryGetValue(armature.FormKey, out var pair))
            {
                ArmorAddon aa = processingInfo.Target.ArmorAddons.GetOrAddAsOverride(armature);
                pair = new KeyValuePair<ArmorAddon, HashSet<FormKey>>(aa, []);
                overriddenArmorAddons.Add(armature.FormKey, pair);
            }
            // prevent adding race twice to AA (if used by multiple armors)
            if (pair.Value.Add(raceInfo.Race.MainRace.FormKey))
            {
                pair.Key.AdditionalRaces.Add(raceInfo.Race.MainRace);
                pair.Key.AdditionalRaces.Add(raceInfo.Race.VampireRace);
            }
        }
        #endregion

        public class ArmorProcessingInfo
        {
            #region properties
            public readonly SkyrimMod Target;
            public readonly IEnumerable<ArmorRaceProcessingInfo> Races;
            public readonly GeneralSettings GenSettings;
            public readonly IEnumerable<Regex> NonWearableArmorRegexes;
            #endregion

            #region ctors
            public ArmorProcessingInfo(SkyrimMod target, IEnumerable<ArmorRaceProcessingInfo> races, GeneralSettings genSettings, IEnumerable<Regex> nonWearableArmorRegexes)
            {
                Target = target;
                Races = races;
                GenSettings = genSettings;
                NonWearableArmorRegexes = nonWearableArmorRegexes;
            }
            #endregion
        }

        public class ArmorRaceProcessingInfo
        {
            #region fields
            public readonly RacePair Race;
            public readonly bool CustomHead;
            public readonly bool CustomBody;
            public readonly bool CustomHands;
            public readonly bool CustomFeet;
            public readonly bool ProcessMale;
            public readonly bool ProcessFemale;
            public readonly IEnumerable<KeyValuePair<Regex, string>> ModelPathReplacers;
            #endregion

            #region ctors
            public ArmorRaceProcessingInfo(RacePair race, bool customHead, bool customBody, bool customHands, bool customFeet, bool procMale, bool procFemale, IEnumerable<KeyValuePair<Regex, string>> modelPathReplacers)
            {
                Race = race;
                CustomHead = customHead;
                CustomBody = customBody;
                CustomHands = customHands;
                CustomFeet = customFeet;
                ProcessMale = procMale;
                ProcessFemale = procFemale;
                ModelPathReplacers = modelPathReplacers;
            }
            #endregion

            #region methods
            public bool CheckIfNeedsCustomArmature(IArmorAddonGetter armature, out KeyValuePair<Regex, string>? maleRegex, out KeyValuePair<Regex, string>? femaleRegex)
            {
                maleRegex = null;
                femaleRegex = null;
                if (armature.WorldModel is null)
                {
                    return false;
                }
                bool flagFound = false;
                if (CustomHead && CheckFirstPersonFlags(armature, BipedObjectFlag.Head)) flagFound = true;
                if (CustomBody && CheckFirstPersonFlags(armature, BipedObjectFlag.Body)) flagFound = true;
                if (CustomHands && CheckFirstPersonFlags(armature, BipedObjectFlag.Hands)) flagFound = true;
                if (CustomFeet && CheckFirstPersonFlags(armature, BipedObjectFlag.Feet)) flagFound = true;
                if (!flagFound)
                {
                    return false;
                }
                if (ProcessMale && armature.WorldModel.Male is not null)
                {
                    var pair = ModelPathReplacers.FirstOrDefault(mpr => mpr.Key.IsMatch(armature.WorldModel.Male.File.GivenPath));
                    if (pair.Key is not null) maleRegex = pair;
                }
                if (ProcessFemale && armature.WorldModel.Female is not null)
                {
                    var pair = ModelPathReplacers.FirstOrDefault(mpr => mpr.Key.IsMatch(armature.WorldModel.Female.File.GivenPath));
                    if (pair.Key is not null) femaleRegex = pair;
                }
                return maleRegex is not null || femaleRegex is not null;
            }
            #endregion

            #region methods (helping)
            private static bool CheckFirstPersonFlags(IArmorAddonGetter aa, BipedObjectFlag flag)
            {
                return (aa.BodyTemplate!.FirstPersonFlags & flag) != 0;
            }
            #endregion
        }

        public readonly struct ProcessedArmorResult(int armorCount, int overwriteAAs, int newAAs, int missingPaths)
        {
            #region fields
            public readonly int ArmorCount = armorCount;
            public readonly int OverwriteAAs = overwriteAAs;
            public readonly int NewAAs = newAAs;
            public readonly int MissingPaths = missingPaths;
            #endregion
        }

        public readonly struct RacePair(Race race, Race vamp)
        {
            #region fields
            public readonly Race MainRace = race;
            public readonly Race VampireRace = vamp;
            #endregion
        }
    }
}
