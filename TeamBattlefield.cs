using System.Collections.Generic;
using System;
using System.Text;
using UnityEngine;
using Oxide.Core;
using Oxide.Core.Plugins;
using System.Reflection; 
using System.Linq;

namespace Oxide.Plugins
{
    [Info("TeamBattlefield", "BodyweightEnergy", "1.1.9", ResourceId = 1330)]
    class TeamBattlefield : RustPlugin
    {
        #region Cached Variables

        private string LogFilename = "";
        private string TeamOneSpawnsFilename;
        private string TeamTwoSpawnsFilename;
        private Dictionary<string, string> displaynameToShortname;
        private Dictionary<ulong, Team> playerTeam;
        private Dictionary<ulong, PlayerStats> playerStats;
        private Dictionary<ulong, DateTime> disconnectTime;
        private float damageScale;
        private string TeamOneShirt;
        private string TeamTwoShirt;
        private object TeamOneSpawnPoint;
        private object TeamTwoSpawnPoint;
        public static FieldInfo lastPositionValue;
        private bool isAdminExempt
        {
            get { return (bool) Config["isAdminExempt"]; }
        }

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
        
        #region Hooks

        void Loaded()
        {
            damageScale = float.Parse(Config["DamageScale"].ToString());
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
            Config["isAdminExempt"] = false;
            Config["TeamOneSpawnfile"] = "tbf_t1_spawns";
            Config["TeamTwoSpawnfile"] = "tbf_t2_spawns";
            Config["DamageScale"] = 0.0f;
            Config["TeamOneShirt"] = "hoodie";
            Config["TeamTwoShirt"] = "tshirt.long";
            Config["TeamOneChatColor"] = "#CC0000";
            Config["TeamTwoChatColor"] = "#0000CC";
            Config["DefaultChatColor"] = "#CCCCCC";
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
            disconnectTime = new Dictionary<ulong, DateTime>();
            playerStats = new Dictionary<ulong, PlayerStats>();
        }

        private void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo hitInfo)
        {
            //Puts("OnEntityTakeDamage() called.");  //for debug purposes
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

        //private void OnPlayerAttack(BasePlayer player, HitInfo hitInfo)
        //{
        //    Puts("OnPlayerAttack() called.");  //for debug purposes
        //    if (hitInfo.HitEntity is BasePlayer && hitInfo.Initiator is BasePlayer)
        //    {
        //        var victimPlayer = (BasePlayer) hitInfo.HitEntity;
        //        //float damageScale = 1.0f;
        //        var sb = new StringBuilder();
        //        if (player == null || hitInfo == null) return;
        //        var attackerPlayer = (BasePlayer)hitInfo.Initiator;
        //        var victimID = victimPlayer.userID;
        //        var attackerID = attackerPlayer.userID;
        //        if (playerTeam.ContainsKey(victimID) && playerTeam.ContainsKey(attackerID))  
        //        {
        //            if (victimID != attackerID)  //Don't modify if attack is suicide
        //            {
        //                if (playerTeam[victimID] == playerTeam[attackerID]) // Modify damage if both attacker and victim are on same team
        //                {
        //                    hitInfo.damageTypes.ScaleAll(damageScale);
        //                    sb.Append("Friendly Fire!");
        //                }
        //            }
        //        }
        //        SendReply(hitInfo.Initiator as BasePlayer, sb.ToString());
        //    }
        //}

        //private void IOnBasePlayerAttacked(BasePlayer player, HitInfo hitInfo)
        //{
        //    Puts("IOnBasePlayerAttacked() was called.");
        //}

        private void OnPlayerInit(BasePlayer player)
        {
            Team team = getTeamForBalance();
            if (player.IsAdmin() && isAdminExempt) { team = Team.SPECTATOR; }    // By-pass team assignment if player is admin
            AssignPlayerToTeam(player, team);
            if (!playerStats.ContainsKey(player.userID)) { playerStats.Add(player.userID, new PlayerStats()); }
            OnPlayerRespawned(player);
        }

        private void OnPlayerDisconnected(BasePlayer player)
        {
            Puts("OnPlayerDisconnected(" + player.displayName + "/" + player.userID.ToString() + ") has been called.");
            ulong ID = player.userID;
            if(playerTeam.ContainsKey(ID))
            {
            	playerTeam.Remove(ID);
            }
            else 
            {
            	Puts("Could not remove disconnected player " + player.displayName + "/" + ID.ToString() + ", userID not found in team list.");
            }
            // // record time of disconnect
            // var currentTime = DateTime.UtcNow;
            // //Puts("OnPlayerDisconnected(): Current time = " + currentTime.ToString());
            // if(!disconnectTime.ContainsKey(player.userID))
            // {
            //     disconnectTime.Add(player.userID, currentTime);
            // }
            // else
            // {
            //     disconnectTime[player.userID] = currentTime;
            // }
            // Puts("OnPlayerDisconnected(): disconnectTime[" + player.displayName + "]=" + disconnectTime[player.userID].ToString());
        }

        private void OnPlayerRespawned(BasePlayer player) 
        {
            if (playerTeam.ContainsKey(player.userID))
            {
                if (playerTeam[player.userID] == Team.SPECTATOR || (isAdminExempt && player.IsAdmin())) return;    //By-pass item giving if player is admin or spectator
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
                    PrintWarning("InvalidCastException on Spawns Database. Please report to plugin developer.");
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

        private bool OnPlayerChat(ConsoleSystem.Arg arg)
        {
            var sb = new StringBuilder();
            BasePlayer player = (BasePlayer)arg.connection.player;
            var playerID = player.userID;
            string message = arg.GetString(0, "text");
            string color = (string)Config["DefaultChatColor"];   //Default name color is white
            if(playerTeam.ContainsKey(playerID))
            {
                switch (playerTeam[playerID])
                {
                    case Team.ONE:
                        color = (string)Config["TeamOneChatColor"];
                        break;
                    case Team.TWO:
                        color = (string)Config["TeamTwoChatColor"];
                        break;
                    default:
                        break;
                }
            }
            sb.Append("<color=" + color + ">");
            sb.Append(player.displayName);
            sb.Append("</color>: ");
            sb.Append(message);

            ChatSay(sb.ToString(), player.userID.ToString());
            return false;
        }

        private void CanBeWounded(BasePlayer player, HitInfo hitInfo)
        {
            if (player != null && hitInfo != null)
            {
                if(hitInfo.Initiator is BasePlayer)
                {
                    var attacker = (BasePlayer) hitInfo.Initiator;
                    var victimID = player.userID;
                    var attackerID = attacker.userID;
                    if(playerStats.ContainsKey(victimID) && playerStats.ContainsKey(attackerID))
                    {
                        playerStats[attackerID].kills++;
                        playerStats[victimID].deaths++;
                    }
                }
            }
        }

        #endregion

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

        [ConsoleCommand("tbf.list")]
        private void cmdList(ConsoleSystem.Arg arg)
        {
            
            var sb = new StringBuilder();
            
            var queryTeamOne = from player in playerTeam
            					where player.Value == Team.ONE
            					select player;
            var queryTeamTwo = from player in playerTeam
								where player.Value == Team.TWO
								select player;
            
            sb.Append("Team ONE:\n");
            sb.Append("KILLS\t\tDEATHS\t\tPLAYER\n");
            foreach (var player in queryTeamOne)
            {
                var playerID = player.Key;
                int playerKills = -1;
                int playerDeaths = -1;
                if (playerStats.ContainsKey(playerID))
                {
                    var stats = playerStats[playerID];
                    playerKills = stats.kills;
                    playerDeaths = stats.deaths;
                }
                var Bplayer = BasePlayer.FindByID(playerID);
                sb.Append(playerKills.ToString().PadLeft(5));
                sb.Append("\t\t");
                sb.Append(playerDeaths.ToString().PadLeft(6));
                sb.Append("\t\t");
                sb.Append(Bplayer.displayName);
                sb.Append("\n");
            }
            sb.Append("\n");
            sb.Append("Team TWO:\n");
            sb.Append("KILLS\t\tDEATHS\t\tPLAYER\n");
            foreach (var player in queryTeamTwo)
			{
				var playerID = player.Key;
                int playerKills = -1;
                int playerDeaths = -1;
                if (playerStats.ContainsKey(playerID))
                {
                    var stats = playerStats[playerID];
                    playerKills = stats.kills;
                    playerDeaths = stats.deaths;
                }
                var Bplayer = BasePlayer.FindByID(playerID);
                sb.Append(playerKills.ToString().PadLeft(5));
                sb.Append("\t\t");
                sb.Append(playerDeaths.ToString().PadLeft(6));
                sb.Append("\t\t");
                sb.Append(Bplayer.displayName);
                sb.Append("\n");
			}
            PrintToConsole(arg.Player(), sb.ToString());
        }

        [ConsoleCommand("tbf.assign")]
        private void cmdAssign(ConsoleSystem.Arg arg)
        {
            var success = true;
            var sb = new StringBuilder();
            var args = arg.Args;
            if(args.Length != 2)
            {
                sb.Append("Format: tbf.assign <PARTIAL_PLAYERNAME> <[\"one\",\"two\",\"spectator\"]>");
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
                    case "spectator":
                        newTeam = Team.SPECTATOR;
                        break;

                    default:
                        sb.Append("Invalid team assignment.");
                        success = false;
                        break;
                }
                if (success)
                {
                    AssignPlayerToTeam(player, newTeam);
                    sb.Append(player.displayName + " has been successfully assigned to team " + newTeamString);
                }
            }
            PrintToConsole(arg.Player(), sb.ToString());
        }

        [ConsoleCommand("tbf.version")]
        private void cmdVersion(ConsoleSystem.Arg arg)
        {
            PrintToConsole(arg.Player(), Version.ToString());
        }

        [ConsoleCommand("tbf.purge")]
        private void cmdPurge(ConsoleSystem.Arg arg)
        {
            int count = KickSleepers();
            PrintToConsole(arg.Player(), "Unassigned a total of " + count.ToString() + " players.");
        }

        [ConsoleCommand("tbf.help")]
        private void cmdHelp(ConsoleSystem.Arg arg)
        {
            var sb = new StringBuilder();
            sb.Append("TeamBattlefield Console Commands:\n\n");
            sb.Append("tbf.list\t\t\tLists Teams and Disconnect Times of players.\n");
            sb.Append("tbf.assign <PartialPlayerName> [one/two/spectator]\t\t\tAssigns player to team.\n");
            sb.Append("tbf.purge\t\t\tRemoves players from all teams if they're been disconnected for more than 5 minutes.\n");
            sb.Append("tbf.version\t\t\tPrints current version number of plugin.\n");

            PrintToConsole(arg.Player(), sb.ToString());
        }

        #endregion

        #region Team Management

        enum Team
        {
            ONE = 1,
            TWO = 2,
            SPECTATOR = 3
        }

        class PlayerStats {
            public int kills;
            public int deaths;

            public PlayerStats ()
            {
                kills = 0;
                deaths = 0;
            }
        };

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
            string teamName = playerTeam[player.userID].ToString();
            if(playerTeam[player.userID] != Team.SPECTATOR) SendReply(player, "You have been assigned to Team " + teamName + ".");
        }

        private int KickSleepers()
        {
            int count = 0;
            foreach(var playerKey in disconnectTime)
            {
                var playerID = playerKey.Key;
                var timeDelta = DateTime.UtcNow.Subtract(playerKey.Value);
                if (timeDelta.TotalMinutes > 5)
                {
                    var player = BasePlayer.FindByID(playerID);
                    if (player == null)
                    {
                        disconnectTime.Remove(playerID);
                        count++;
                    }
                    else if (!BasePlayer.FindByID(playerID).IsAlive() && playerTeam.ContainsKey(playerID))
                    {
                        playerTeam.Remove(playerID);
                        disconnectTime.Remove(playerID);
                        count++;
                    }
                }
            }
            Puts("Kicked a total of " + count.ToString() + " sleepers.");
            return count;
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

        public void ChatSay(string message, string userid = "0")
        {
            ConsoleSystem.Broadcast("chat.add", userid, message, 1.0);
        }

        #region Cross-Plugin Functions

        int GetPlayerTeam (ulong playerID)
        {
            if(playerTeam.ContainsKey(playerID))
            {
                Team team = playerTeam[playerID];
                switch (team) 
                {
                    case Team.ONE:
                        return 1;
                    case Team.TWO:
                        return 2;
                    case Team.SPECTATOR:
                        return 3;
                    default:
                        return 0;
                }
            }
            else
            {
                return 0;
            }
        }
        Dictionary<ulong, int> GetTeams()
        {
            Dictionary<ulong, int> returnedList = new Dictionary<ulong, int>();
            foreach( var player in playerTeam)
            {
                returnedList.Add(player.Key, (int)player.Value);
            }
            return returnedList;
        }

        #endregion
    }
}
