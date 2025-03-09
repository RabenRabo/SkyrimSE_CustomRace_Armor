SkyrimSE_CustomRaceArmor

This is a Skyrim Special Edition patcher software to create a ESP plugin (using the Mutagen framework) which removes the ArmorRace (typically DefaultRace) of custom race(s) and patches the ArmorAddons of all Armor found in the load order to either apply to the custom race(s) or attaches new ArmorAddons with new model paths.
The GeneralSettings.json file contains a list of EditorIDs of Armor that will be ignored by the patcher.
The RaceSettings folder contains JSON files with patching settings for specific races.
NOTE: this software only creates an ESP plugin file, NOT actual meshes (NIF files) which are referenced in said ESP.

SOURCE
Sources for this software are published at https://github.com/RabenRabo/SkyrimSE_CustomRace_Armor

LICENSES

This software is licensed under the MIT license (see LICENSE.txt)
The Mutagen framework is licensed under the GPLv3 license (see Licenses/Mutagen_LICENSE.txt)