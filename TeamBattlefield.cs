using System.Collections.Generic;
using System;
using System.Text;
using UnityEngine;
using Oxide.Core;
using Oxide.Core.Plugins;
using System.Reflection;

namespace Oxide.Plugins
{
    [Info("TeamBattlefield", "BodyweightEnergy", "1.1.3", ResourceId = 1330)]
    class TeamBattlefield : RustPlugin
    {
        #region Cached Variables

        private string TeamOneSpawnsFilename;
        private string TeamTwoSpawnsFilename;
        private Dictionary<string, string> displaynameToShortname;
        private Dictionary<ulong, Team> playerTeam;
        private float damageScale;
        private string TeamOneShirt;
        private string TeamTwoShirt;
        private object TeamOneSpawnPoint;
        private object TeamTwoSpawnPoint;
        public static FieldInfo lastPositionValue;

        #endregion

        // Default Constructor
        public TeamBattlefield ()
        {
            displaynameToShortname = new Dictionary<string, string>();
            playerTeam = new Dictionary<ulong, Team>();
            TeamOneSpawnPoint = new object();
            TeamTwoSpawnPoint = new object();
            damageScale = new float();
        }

        // Reference to Spawns Database
        [PluginReference("Spawns")]
        Plugin SpawnsDatabase;

        void Loaded()
        {
            object damageScaleFriendlyObj = Config["DamageScale"];
            damageScale = float.Parse(damageScaleFriendlyObj.ToString());
            Puts("Friendly-fire damage scaled to " + damageScale.ToString("0.000"));
            TeamOneShirt = Config["TeamOneShirt"] as string;
            TeamTwoShirt = Config["TeamTwoShirt"] as string;
            TeamOneSpawnsFilename = Config["TeamOneSpawnfile"] as string;
            TeamTwoSpawnsFilename = Config["TeamTwoSpawnfile"] as string;
            lastPositionValue = typeof(BasePlayer).GetField("lastPositionValue", (BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic));
        }

        // Default Configuration
        protected override void LoadDefaultConfig()
        {
            PrintWarning("Creating a new configuration file.");
            Config.Clear();
            Config["TeamOneSpawnfile"] = "tbf_t1_spawns";
            Config["TeamTwoSpawnfile"] = "tbf_t2_spawns";
            Config["DamageScale"] = 0.0f;
            Config["TeamOneShirt"] = "hoodie";
            Config["TeamTwoShirt"] = "tshirt.long";
            var common_items_belt = new Dictionary<string, object>();
            var common_items_wear = new Dictionary<string, object>();
            var common_items_main = new Dictionary<string, object>();
            common_items_wear.Add("pants", 1);
            common_items_wear.Add("riot.helmet", 1);
            common_items_wear.Add("metal.plate.chest", 1);
            common_items_wear.Add("shoes.boots", 1);
            common_items_wear.Add("metal.plate.torso", 1);
            common_items_belt.Add("rifle.ak", 1);
            common_items_belt.Add("pistol.semiauto", 1);
            common_items_belt.Add("machete", 1);
            common_items_belt.Add("largemedkit", 3);
            common_items_belt.Add("syringe.medical", 3);
            common_items_belt.Add("rifle.bolt", 1);
            common_items_main.Add("ammo.rifle.hv", 200);
            common_items_main.Add("ammo.pistol.hv", 200);
            common_items_main.Add("hatchet", 1);
            common_items_main.Add("pickaxe", 1);
            common_items_main.Add("grenade", 1);
            Config["common_items_belt"] = common_items_belt;
            Config["common_items_wear"] = common_items_wear;
            Config["common_items_main"] = common_items_main;
            SaveConfig();
        }

        private void OnServerInitialized()
        {
            playerTeam = new Dictionary<ulong, Team>();
        }

        private void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo hitInfo)
        {
            //if (entity == null || hitInfo == null) return;
            if (entity is BasePlayer && hitInfo.Initiator is BasePlayer)
            {
                //float damageScale = 1.0f;
                var sb = new StringBuilder();
                if (entity as BasePlayer == null || hitInfo == null) return;
                var attackerPlayer = (BasePlayer)hitInfo.Initiator;
                var victimPlayer = (BasePlayer)entity;
                var victimID = victimPlayer.userID;
                var attackerID = attackerPlayer.userID;
                if (playerTeam.ContainsKey(victimID) && playerTeam.ContainsKey(attackerID))
                {
                    if (victimID != attackerID)
                    {
                        if (playerTeam[victimID] == playerTeam[attackerID])
                        {
                            hitInfo.damageTypes.ScaleAll(damageScale);
                            sb.Append("Friendly Fire!");
                        }
                    }
                }
                SendReply(hitInfo.Initiator as BasePlayer, sb.ToString());
            }
        }

        private void OnPlayerInit(BasePlayer player)
        {
            Team team = getTeamForBalance();
            playerTeam.Add(player.userID, team);
            string teamName = playerTeam[player.userID].ToString();
            SendReply(player, "You have been assigned to Team " + teamName + ".");
            Puts("Player " + player.displayName + " assigned to Team " + teamName + ".");
            OnPlayerRespawned(player);
        }

        private void OnPlayerDisconnected(BasePlayer player)
        {
            ulong ID = player.userID;
            if (playerTeam.ContainsKey(ID)) playerTeam.Remove(ID);
        }

        private void OnPlayerRespawned(BasePlayer player)
        {
            if (playerTeam.ContainsKey(player.userID))
            {
                try
                {
                    // Spawns Database functionality
                    if (SpawnsDatabase != null)
                    {
                        var loadFile1Status = SpawnsDatabase.Call("loadSpawnfile", (string) Config["TeamOneSpawnfile"]);
                        var loadFile2Status = SpawnsDatabase.Call("loadSpawnfile", (string) Config["TeamTwoSpawnfile"]);
                        if (loadFile1Status is string || loadFile2Status is string )
                        {
                            PrintWarning("Correct spawn files not found.");
                        }
                        else
                        {
                            Vector3 spawnPoint = (Vector3)GetSpawnPoint(player);
                            ForcePlayerPosition(player, spawnPoint);
                            Puts("{0} spawned using Spawns Database, to position {1}", player.displayName, spawnPoint.ToString());
                        }
                    }
                } catch (InvalidCastException ex)
                {
                    PrintWarning("InvalidCastException on Spawns Database.");
                }
                //Puts("Entered OnPlayerRespawned. Player team is " + playerTeam[player.userID].ToString());
                player.inventory.Strip();
                var common_item_wear = (Dictionary<string, object>) Config["common_items_wear"];
                foreach (var item in common_item_wear)
                {
                    GiveItem(player, item.Key, (int) item.Value, player.inventory.containerWear);
                }
                var common_item_belt = (Dictionary<string, object>) Config["common_items_belt"];
                foreach (var item in common_item_belt)
                {
                    GiveItem(player, item.Key, (int) item.Value, player.inventory.containerBelt);
                }
                var common_item_main = (Dictionary<string, object>) Config["common_items_main"];
                foreach (var item in common_item_main)
                {
                    GiveItem(player, item.Key, (int) item.Value, player.inventory.containerMain);
                }
                if (playerTeam[player.userID] == Team.ONE)
                {
                    GiveItem(player, TeamOneShirt, 1, player.inventory.containerWear);
                }
                else if (playerTeam[player.userID] == Team.TWO)
                {
                    GiveItem(player, TeamTwoShirt, 1, player.inventory.containerWear);
                }
            }
            else
            {
                OnPlayerInit(player);
            }
        }

        #region Kits

        private void InitializeTable()
        {
            displaynameToShortname.Clear();
            List<ItemDefinition> ItemsDefinition = ItemManager.GetItemDefinitions() as List<ItemDefinition>;
            foreach (ItemDefinition itemdef in ItemsDefinition)
            {
                displaynameToShortname.Add(itemdef.displayName.english.ToString().ToLower(), itemdef.shortname.ToString());
            }
        }

        public object GiveItem(BasePlayer player, string itemname, int amount, ItemContainer pref)
        {
            itemname = itemname.ToLower();

            bool isBP = false;
            if (itemname.EndsWith(" bp"))
            {
                isBP = true;
                itemname = itemname.Substring(0, itemname.Length - 3);
            }
            if (displaynameToShortname.ContainsKey(itemname))
                itemname = displaynameToShortname[itemname];
            var definition = ItemManager.FindItemDefinition(itemname);
            if (definition == null)
                return string.Format("{0} {1}", "Item not found: ", itemname);
            int giveamount = 0;
            int stack = (int)definition.stackable;
            if (isBP)
                stack = 1;
            if (stack < 1) stack = 1;
            for (var i = amount; i > 0; i = i - stack)
            {
                if (i >= stack)
                    giveamount = stack;
                else
                    giveamount = i;
                if (giveamount < 1) return true;
                player.inventory.GiveItem(ItemManager.CreateByItemID((int)definition.itemid, giveamount, isBP), pref);
            }
            return true;
        }

        #endregion

        #region Console Commands

        [ConsoleCommand("tbf.dump")]
        private void cmdDump(ConsoleSystem.Arg arg)
        {
            
            var sb = new StringBuilder();
            sb.Append("Team Data Dump:\n");
            foreach (var player in playerTeam)
            {
                var Bplayer = BasePlayer.FindByID(player.Key);
                //sb.Append(player.Key);
                //sb.Append(": ");
                sb.Append(player.Value.ToString());
                sb.Append("\t\t");
                sb.Append(Bplayer.displayName);
                sb.Append("\n");
            }
            PrintToConsole(arg.Player(), sb.ToString());
        }

        [ConsoleCommand("tbf.assign")]
        private void cmdAssign(ConsoleSystem.Arg arg)
        {
            var sb = new StringBuilder();
            var args = arg.Args;
            if(args.Length != 2)
            {
                sb.Append("Format: tbf.assign <PARTIAL_PLAYERNAME> <[\"ONE\"/\"TWO\"]>");
            }
            else if(arg.connection.authLevel < 1)
            {
                sb.Append("You don't have permission to assign players.");
            }
            else
            {
                var partialPlayerName = args[0];
                var player = FindPlayerByPartialName(partialPlayerName);
                var newTeamString = args[1].ToLower();
                var newTeam = Team.SPECTATOR;
                switch(newTeamString)
                {
                    case "one":
                        newTeam = Team.ONE;
                        break;

                    case "two":
                        newTeam = Team.TWO;
                        break;

                    default:
                        newTeam = Team.SPECTATOR;
                        break;
                }
                AssignPlayerToTeam(player, newTeam);
                sb.Append(player.displayName + " has been successfully assigned to team " + newTeamString);
            }
            PrintToConsole(arg.Player(), sb.ToString());
        }

        #endregion

        #region Team Management

        enum Team
        {
            ONE,
            TWO,
            SPECTATOR
        }
        private int TeamCount(Team team)
        {
            int count = 0;
            foreach (var player in playerTeam)
            {
                if (player.Value == team)
                    count++;
            }
            return count;
        }

        // Returns the proper team to maintain team balance
        private Team getTeamForBalance()
        {
            Team returnedTeam = Team.SPECTATOR;
            int teamOneCount = TeamCount(Team.ONE);
            int teamTwoCount = TeamCount(Team.TWO);

            if (teamOneCount > teamTwoCount)
            {
                returnedTeam = Team.TWO;
            }
            else
            {
                returnedTeam = Team.ONE;
            }
            return returnedTeam;
        }

        private object GetSpawnPoint(BasePlayer player)
        {
            var filename = "";
            var team = playerTeam[player.userID];
            if (team == Team.ONE) filename = Config["TeamOneSpawnfile"].ToString();
            else if (team == Team.TWO) filename = Config["TeamTwoSpawnfile"].ToString();
            var spawnPoint = SpawnsDatabase.Call("GetRandomSpawn", new object[] {filename});
            return spawnPoint;
        }

        private BasePlayer FindPlayerByPartialName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return null;
            BasePlayer player = null;
            name = name.ToLower();
            var allPlayers = BasePlayer.activePlayerList.ToArray();
            // Try to find an exact match first
            foreach (var p in allPlayers)
            {
                if (p.displayName == name)
                {
                    if (player != null)
                        return null; // Not unique
                    player = p;
                }
            }
            if (player != null)
                return player;
            // Otherwise try to find a partial match
            foreach (var p in allPlayers)
            {
                if (p.displayName.ToLower().IndexOf(name) >= 0)
                {
                    if (player != null)
                        return null; // Not unique
                    player = p;
                }
            }
            return player;
        }

        private void AssignPlayerToTeam(BasePlayer player , Team newTeam)
        {
            playerTeam[player.userID] = newTeam;
        }

        #endregion

        static void ForcePlayerPosition(BasePlayer player, Vector3 destination)
        {
            //PutToSleep(player);
            player.transform.position = destination;
            lastPositionValue.SetValue(player, player.transform.position);
            player.ClientRPCPlayer(null, player, "ForcePositionTo", new object[] { destination });
            player.TransformChanged();

            player.SetPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot, true);
            player.UpdateNetworkGroup();

            player.SendNetworkUpdateImmediate(false);
            player.ClientRPCPlayer(null, player, "StartLoading");
            player.SendFullSnapshot();
            player.SetPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot, false);
            player.ClientRPCPlayer(null, player, "FinishLoading");
        }
    }
}
