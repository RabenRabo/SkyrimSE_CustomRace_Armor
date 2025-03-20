using DynamicData;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Environments;
using Mutagen.Bethesda.FormKeys.SkyrimSE;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Skyrim;
using System.Text.RegularExpressions;

namespace SSE.CRA.BL
{
    public class Modding : IDisposable
    {
        #region fields
        private readonly IGameEnvironment<ISkyrimMod, ISkyrimModGetter> _environment;
        #endregion

        #region ctors
        public Modding(SkyrimRelease version)
        {
            _environment = GameEnvironment.Typical.Skyrim(version);
        }
        public Modding(SkyrimRelease version, string customGameDataPath)
        {
            _environment = GameEnvironment.Typical.Builder<ISkyrimMod, ISkyrimModGetter>(GameRelease.SkyrimSE)
                .WithTargetDataFolder(customGameDataPath).Build();
        }
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
        public CustomRacesResult GetRaces()
        {
            HashSet<string> res = [];
            int vanilla = 0;
            foreach (var raceGetter in _environment.LoadOrder.PriorityOrder.OnlyEnabled().Race().WinningOverrides())
            {
                // skip if no EditorID (?!)
                if (raceGetter.EditorID is null) continue;
                // count vanilla races / collect non-vanilla
                if(raceGetter.FormKey.ModKey == Skyrim.ModKey || raceGetter.FormKey.ModKey == Dawnguard.ModKey || raceGetter.FormKey.ModKey == Dragonborn.ModKey) vanilla++;
                else res.Add(raceGetter.EditorID);
            }
            return new(res, vanilla);
        }
        /// <summary>
        /// Removes ArmorRace from race and its additional races, and updates Race and AdditionalRaces of naked skin
        /// </summary>
        /// <param name="processingInfo"></param>
        /// <param name="target"></param>
        public void ProcessRaces(ArmorProcessingInfo processingInfo)
        {
            var mod = processingInfo.GetMod();
            foreach (var raceInfo in processingInfo.Races)
            {
                raceInfo.Race.Getter = GetRace(raceInfo.Race.EditorID);
                // set ArmorRace of race to None (as opposed to DefaultRace)
                Race ovRace = mod.Races.GetOrAddAsOverride(raceInfo.Race.Getter);
                ovRace.ArmorRace.Clear();
                foreach (var addRace in raceInfo.AdditionalRaces)
                {
                    addRace.Getter = GetRace(addRace.EditorID);
                    // set ArmorRace of race to None (as opposed to DefaultRace)
                    ovRace = mod.Races.GetOrAddAsOverride(addRace.Getter);
                    ovRace.ArmorRace.Clear();
                }
                // change race of skin armor
                IArmorGetter skinArmor = raceInfo.Race.Getter.Skin.Resolve(_environment.LinkCache);
                foreach (IArmorAddonGetter armature in skinArmor.Armature.Select(a => a.Resolve(_environment.LinkCache)))
                {
                    if (CheckIfNakedSkinArmature(armature, raceInfo))
                    {
                        ArmorAddon aa = mod.ArmorAddons.GetOrAddAsOverride(armature);
                        aa.Race = raceInfo.Race.Getter.ToNullableLink();
                        aa.AdditionalRaces.Clear();
                        foreach(var addRace in raceInfo.AdditionalRaces)
                        {
                            aa.AdditionalRaces.Add(addRace.Key);
                        }
                    }
                }
            }
        }
        /// <summary>
        /// Collects all Armor and ArmorAddons to be processed
        /// </summary>
        /// <returns></returns>
        public PreProcessResult PreProcessArmor(ArmorProcessingInfo processingInfo)
        {
            var result = new PreProcessResult();
            foreach (var armorGetter in _environment.LoadOrder.PriorityOrder.OnlyEnabled().Armor().WinningOverrides())
            {
                if (processingInfo.IsCancellationRequested) break;
                // check if should ignore based on EditorID of armor
                if (processingInfo.GenSettings.IgnoreEditorIDs.Contains(armorGetter.EditorID!)) continue;
                // check if armor is wearable or something weird instead
                if (!CheckIfWearableArmor(armorGetter)) continue;
                ArmorInfo? armorInfo = null;
                foreach (var arm in armorGetter.Armature)
                {
                    ArmorAddonInfo? armorAddonInfo = null;
                    if (processingInfo.IsCancellationRequested) break;
                    foreach (var raceInfo in processingInfo.Races)
                    {
                        if (processingInfo.IsCancellationRequested) break;
                        IArmorAddonGetter aa = arm.Resolve(_environment.LinkCache);
                        // check if ArmorAddon is for DefaultRace or should skip this one
                        if (aa.Race.FormKey != Skyrim.Race.DefaultRace.FormKey && !aa.AdditionalRaces.Any(ar => ar.FormKey == Skyrim.Race.DefaultRace.FormKey)) continue;

                        // armor will be processed in some way
                        if (armorInfo is null)
                        {
                            armorInfo = new ArmorInfo(armorGetter.FormKey, armorGetter);
                            result.Armors.Add(armorGetter.FormKey, armorInfo);
                        }
                        // armor addon will be processed in some way
                        if (armorAddonInfo is null && !result.ArmorAddons.TryGetValue(aa.FormKey, out armorAddonInfo))
                        {
                            armorAddonInfo = new ArmorAddonInfo(aa.FormKey, aa);
                            result.ArmorAddons.Add(aa.FormKey, armorAddonInfo);
                        }
                        armorInfo.ArmorAddonKeys.Add(aa.FormKey);
                        armorAddonInfo.Armors.Add(armorInfo);
                        // check if ArmorAddon was already marked for processing by a previous Armor
                        if (armorAddonInfo.Races.TryGetValue(raceInfo.Race.Key, out ArmorAddonRaceInfo? aaRaceInfo)) continue;
                        if (raceInfo.CheckIfNeedsCustomArmature(aa, out ModelPathRegexInfo? maleRegex, out ModelPathRegexInfo? femaleRegex))
                        {
                            aaRaceInfo = new ArmorAddonRaceNewInfo(armorAddonInfo, raceInfo.Race, raceInfo.AdditionalRaces)
                            {
                                Male = maleRegex,
                                Female = femaleRegex,
                            };
                        }
                        else
                        {
                            aaRaceInfo = new ArmorAddonRaceExtInfo(armorAddonInfo, raceInfo.Race, raceInfo.AdditionalRaces);
                        }
                        armorAddonInfo.Races.Add(raceInfo.Race.Key, aaRaceInfo);
                    }
                }
            }
            return result;

        }
        /// <summary>
        /// Generates overrides/creates new records based on the PreProcessResult
        /// </summary>
        /// <param name="processingInfo"></param>
        /// <param name="preProcessResult"></param>
        public ProcessedArmorResult ProcessArmor(ArmorProcessingInfo processingInfo, PreProcessResult preProcessResult)
        {
            int armorCount = 0;
            int overriddenAACount = 0;
            int newAACount = 0;
            foreach (var armor in preProcessResult.Armors.Values)
            {
                // check if armor already assigned to group, i.e. written to an output mod
                if (armor.AssignedToGroup) continue;
                var group = preProcessResult.CompileArmorGroupInfo(armor);
                var output = processingInfo.GetMod(group);
                group.WriteTo(processingInfo, output);
                // update statistics
                armorCount += group.Armor.Count();
                overriddenAACount += group.OVerrideRecords;
                newAACount += group.NewRecords;
            }
            processingInfo.WriteOutputMods();
            return new(armorCount, overriddenAACount, newAACount, processingInfo.MissingModelPaths.Count, processingInfo.CreatedFiles);
        }
        public void Dispose()
        {
            _environment.Dispose();
        }
        #endregion

        #region methods (helping)
        private IRaceGetter GetRace(string editorID)
        {
            return _environment.LoadOrder.PriorityOrder.Race().WinningOverrides().First(r => r.EditorID == editorID);
        }
        private bool CheckIfNakedSkinArmature(IArmorAddonGetter aa, ArmorRaceProcessingInfo raceInfo)
        {
            // check if ArmorAddon.Race is set to DefaultRace and ArmorAddon.AdditionalRaces contains the given race and its additional races
            return aa.Race.FormKey == Skyrim.Race.DefaultRace.FormKey && aa.AdditionalRaces.Count == 1 + raceInfo.AdditionalRaces.Count() && aa.AdditionalRaces.Contains(raceInfo.Race.Key) && raceInfo.AdditionalRaces.All(ar => aa.AdditionalRaces.Contains(ar.Key));
        }
        private bool CheckIfWearableArmor(IArmorGetter armorGetter)
        {
            return armorGetter.EditorID is not null && !armorGetter.EditorID.StartsWith("SkinNaked");
        }
        #endregion

        public class ArmorProcessingInfo
        {
            #region fields
            public readonly string GameDataPath;
            public readonly string OutputName;
            public readonly bool FlagESL;
            public readonly int MaxMasters;
            public readonly int MaxNewRecords;
            public readonly GeneralSettings GenSettings;
            public readonly IEnumerable<Regex> NonWearableArmorRegexes;
            private readonly List<OutputMod> Outputs = [];
            private readonly HashSet<string> _missingModelPaths = [];
            #endregion

            #region properties
            public IEnumerable<string> CreatedFiles => [.. Outputs.Select(o => o.Output.ModKey.FileName.String).Order()];
            public IEnumerable<ArmorRaceProcessingInfo> Races { get; set; } = [];
            public IProgress<ProgressInfo>? Progress { get; set; }
            public CancellationToken? CancellationToken { get; set; }
            public bool IsCancellationRequested => CancellationToken.HasValue && CancellationToken.Value.IsCancellationRequested;
            public HashSet<string> MissingModelPaths => _missingModelPaths;
            #endregion

            #region ctors
            public ArmorProcessingInfo(string gameDataPath, string outputName, bool flagESL, int maxMasters, int maxNewRecords, GeneralSettings genSettings, IEnumerable<Regex> nonWearableArmorRegexes)
            {
                GameDataPath = gameDataPath;
                OutputName = outputName;
                FlagESL = flagESL;
                MaxMasters = maxMasters;
                MaxNewRecords = maxNewRecords;
                GenSettings = genSettings;
                NonWearableArmorRegexes = nonWearableArmorRegexes;
            }
            #endregion

            #region methods
            public SkyrimMod GetMod()
            {
                Outputs.Sort();
                OutputMod? output = Outputs.FirstOrDefault();
                output ??= GetNewOutput();
                return output.Output;
            }
            public SkyrimMod GetMod(ArmorGroupInfo groupInfo)
            {
                if (FlagESL && groupInfo.NewRecords > MaxNewRecords) throw new ArgumentException("group of Armor/ArmorAddons has too many new records");
                if (groupInfo.RequiredMasters.Count > MaxMasters) throw new ArgumentException("group of Armor/ArmorAddons requires too many plugin masters");
                Outputs.Sort();
                OutputMod? output = Outputs.FirstOrDefault(m => (!FlagESL || m.NewRecordCount + groupInfo.NewRecords <= MaxNewRecords) && m.Masters.Union(groupInfo.RequiredMasters).Count() <= MaxMasters);
                output ??= GetNewOutput();
                output.NewRecordCount += groupInfo.NewRecords;
                output.Masters.UnionWith(groupInfo.RequiredMasters);
                return output.Output;
            }
            public void WriteOutputMods()
            {
                foreach (var output in Outputs)
                {
                    output.Output.BeginWrite
                        .ToPath(Path.Combine(GameDataPath, output.Output.ModKey.FileName.String))
                        .WithDefaultLoadOrder()
                        .WriteAsync().Wait();
                }
            }
            #endregion

            #region methods (helping)
            private OutputMod GetNewOutput()
            {
                var modKey = new ModKey($"{OutputName}{Outputs.Count + 1:D2}", ModType.Plugin);
                var mod = new SkyrimMod(modKey, SkyrimRelease.SkyrimSE);
                if (FlagESL) mod.ModHeader.Flags |= SkyrimModHeader.HeaderFlag.Small;
                var output = new OutputMod(mod);
                Outputs.Add(output);
                return output;
            }
            #endregion

            private class OutputMod : IComparable<OutputMod>
            {
                #region fields
                public readonly SkyrimMod Output;
                #endregion

                #region properties
                public int NewRecordCount { get; set; }
                public HashSet<ModKey> Masters { get; set; } = [];
                #endregion

                #region ctors
                public OutputMod(SkyrimMod output)
                {
                    Output = output;
                }
                #endregion

                #region methods
                public int CompareTo(OutputMod? output)
                {
                    return NewRecordCount - (output?.NewRecordCount ?? 0);
                }
                #endregion
            }
        }

        public class ArmorRaceProcessingInfo
        {
            #region fields
            public readonly RaceID Race;
            public readonly bool CustomHead;
            public readonly bool CustomBody;
            public readonly bool CustomHands;
            public readonly bool CustomFeet;
            public readonly bool ProcessMale;
            public readonly bool ProcessFemale;
            public readonly IEnumerable<ModelPathRegexInfo> ModelPathReplacers;
            public readonly IEnumerable<RaceID> AdditionalRaces;
            #endregion

            #region ctors
            public ArmorRaceProcessingInfo(RaceID race, bool customHead, bool customBody, bool customHands, bool customFeet, bool procMale, bool procFemale, IEnumerable<ModelPathRegexInfo> modelPathReplacers, IEnumerable<RaceID> additionalRaces)
            {
                Race = race;
                CustomHead = customHead;
                CustomBody = customBody;
                CustomHands = customHands;
                CustomFeet = customFeet;
                ProcessMale = procMale;
                ProcessFemale = procFemale;
                ModelPathReplacers = modelPathReplacers;
                AdditionalRaces = additionalRaces;
            }
            #endregion

            #region methods
            public bool CheckIfNeedsCustomArmature(IArmorAddonGetter armature, out ModelPathRegexInfo? maleRegex, out ModelPathRegexInfo? femaleRegex)
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
                    maleRegex = ModelPathReplacers.FirstOrDefault(mpr => mpr.Regex.IsMatch(armature.WorldModel.Male.File.GivenPath));
                }
                if (ProcessFemale && armature.WorldModel.Female is not null)
                {
                    femaleRegex = ModelPathReplacers.FirstOrDefault(mpr => mpr.Regex.IsMatch(armature.WorldModel.Female.File.GivenPath));
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

        public readonly struct ProcessedArmorResult(int armorCount, int overriddenAACount, int newAACount, int missingPathCount, IEnumerable<string> createdFiles)
        {
            #region fields
            public readonly int ArmorCount = armorCount;
            public readonly int OverriddenAACount = overriddenAACount;
            public readonly int NewAACount = newAACount;
            public readonly int MissingPathCount = missingPathCount;
            public readonly IEnumerable<string> CreatedFiles = createdFiles;
            #endregion
        }

        public readonly struct CustomRacesResult(IEnumerable<string> raceEditorIDs, int vanillaRacesCount)
        {
            #region fields
            public readonly IEnumerable<string> RaceEditorIDs = raceEditorIDs;
            public readonly int VanillaRacesCount = vanillaRacesCount;
            #endregion
        }

        public class RaceID
        {
            #region fields
            public readonly string EditorID;
            #endregion

            #region properties
            public FormKey Key => Getter?.FormKey ?? new();
            public IRaceGetter? Getter { get; set; }
            #endregion

            #region ctors
            public RaceID(string editorID)
            {
                EditorID = editorID;
            }
            #endregion
        }
    }
}
