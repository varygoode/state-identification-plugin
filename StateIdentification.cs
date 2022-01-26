using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Game.Rust;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Rust;
using UnityEngine;

namespace Oxide.Plugins
{
	[Info("State Identification", "varygoode", "1.0.0")]
	[Description("Give players a unique state identification number and card")]

	internal class StateIdentification : CovalencePlugin
	{
		#region Fields

		private const string PermAdmin = "stateidentification.admin";
		private const string PermUse = "stateidentification.use";

		private StoredData storedData;
        private Configuration config;

		#endregion Fields

		#region Init

        private void Init()
        {
            permission.RegisterPermission(PermAdmin, this);
            permission.RegisterPermission(PermUse, this);

            storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(Name);

            var lastStateID = storedData.StateIDs.Values.Select(l => l.Last()).OrderByDescending(t => t.Number).FirstOrDefault();
            StateID.CurrentID = lastStateID != null ? lastStateID.Number : 0;
        }

        #endregion Init

        #region Hooks

        private void Loaded()
        {
            
        }

        private void OnServerSave() => SaveData();

        private void Unload() => SaveData();

        private void OnPlayerConnected(BasePlayer player)
        {
            IPlayer idHolder = player.IPlayer;

            if (!idHolder.HasPermission(PermUse)) return;

            StateID newStateID = new StateID(player.displayName.ToUpper(), " ", "CARDWAITHE");

            if (storedData.StateIDs.ContainsKey(idHolder.Id))
            {
                if (storedData.StateIDs[idHolder.Id].IsEmpty())
                {
                    storedData.StateIDs[idHolder.Id].Add(newStateID);
                    idHolder.Reply(Lang("OnConnected", idHolder.Id, player.displayName, newStateID.Number.ToString()));
                    return;
                }                
            }
            else
            {
                storedData.StateIDs.Add(idHolder.Id, new List<StateID>() { newStateID });
                idHolder.Reply(Lang("OnConnected", idHolder.Id, player.displayName, newStateID.Number.ToString()));
                return;
            }
        }

        #endregion Hooks

        #region Localization

        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NoUse"] = "You are not permitted to use that command.",
                ["NoID"] = "You do not have your state identification set up.",
                ["NotFound"] = "Person or ID not found!",
                ["Exists"] = "State ID already exists for {0}. Were you trying to update? Use /stateid update",
                ["NoShow"] = "No player with name or ID {0} exists.",
                ["Separator"] = "-----------------------------",
                ["OnConnected"] = "Hello {0}! Your new state ID # is {1}. Please seek a god for corrections/updates. Use /stateid for more info.",

                ["StateIDInfo0"] = "Your state identification provides you a unique number by which the realm can identify you.\n"
                                   + "To show your ID to someone with Name, use /stateid show Name",

                ["ShowUsage"] = "To show to another player, use /stateid show <Name or StateID#>",
                ["CreateUsage"] = "Usage: /stateid create FirstName LastName \"Home Realm\"",
                ["UpdateUsage"] = "Usage: /stateid update StateID# NewFirstName NewLastName \"New Home Realm\"",
                ["DeleteUsage"] = "Usage: /stateid delete StateID#",

                ["Create_Success"] = "ID #{0} successfully created.",
                ["Update_Success"] = "ID #{0} successfully updated.",
                ["Delete_Success"] = "ID #{0} successfully deleted.",
                ["Wipe_Success"] = "All State Identification data successfully wiped."
            }, this);
        }

        #endregion Localization

        #region Commands

        [Command("stateid")]
        private void CommandStateID(IPlayer iPlayer, string command, string[] args)
        {
        	if (!iPlayer.HasPermission(PermUse))
        	{
        		iPlayer.Reply(Lang("NoUse", iPlayer.Id, command));
                return;
        	}

        	if (args.Length < 1)
        	{
        		var message = "Usage: /stateid info|show";
                if (iPlayer.HasPermission(PermAdmin)) message += "|create|update|delete|wipe";

                iPlayer.Message(message);
                return;
        	}

        	switch (args[0].ToLower())
        	{
        		case "info":
        		    iPlayer.Reply(Lang("StateIDInfo0", iPlayer.Id, command));

        		    return;

        		case "show":
                    if (!storedData.StateIDs.ContainsKey(iPlayer.Id) || storedData.StateIDs[iPlayer.Id].IsEmpty())
                    {
                        iPlayer.Reply(Lang("NoID", iPlayer.Id, command));
                        return;
                    }

                    StateID idToShow = storedData.StateIDs[iPlayer.Id].First();

                    string message = "-----------------------------\n" +
                                     "|STATE ID #" +$"{idToShow.Number}|\n" +
                                     "|Last Name: " + $"{idToShow.LastName}|\n" +
                                     "|First Name: " + $"{idToShow.FirstName}|\n" +
                                     "|Home Realm: " + $"{idToShow.HomeRealm}|\n" +
                                     "-----------------------------";

                    iPlayer.Reply(message);

                    if (args.Length < 2)
                    {
                        iPlayer.Reply(Lang("ShowUsage", iPlayer.Id, command));
                        return;
                    }

                    var playerToShow = FindPlayer(args[1]);

                    if (playerToShow == null)
                    {
                        iPlayer.Reply(Lang("NoShow", iPlayer.Id, args[1]));
                        return;
                    }

                    playerToShow.Reply(message);

        		    return;

                case "create":
                    if (!iPlayer.HasPermission(PermAdmin))
                    {
                        iPlayer.Reply(Lang("NoUse", iPlayer.Id, command));
                        return;
                    }

                    if (args.Length < 4)
                    {
                        iPlayer.Reply(Lang("CreateUsage", iPlayer.Id, command));
                        return;
                    }

                    var idHolder = FindPlayer(args[1]);

                    if (idHolder == null)
                    {
                        iPlayer.Reply(Lang("NotFound", iPlayer.Id, command));
                        return;
                    }

                    var newStateID = new StateID(args[1].ToUpper(), args[2].ToUpper(), args[3].ToUpper());

                    if (storedData.StateIDs.ContainsKey(idHolder.Id))
                    {
                        if (!storedData.StateIDs[idHolder.Id].IsEmpty())
                        {
                            iPlayer.Reply(Lang("Exists", iPlayer.Id, (idHolder.Object as BasePlayer).displayName));
                            return;
                        }

                        storedData.StateIDs[iPlayer.Id].Add(newStateID);
                    }
                    else
                    {
                        storedData.StateIDs.Add(idHolder.Id, new List<StateID>() { newStateID });
                    }

                    iPlayer.Reply(Lang("Create_Success", iPlayer.Id, newStateID.Number.ToString()));

                    return;

                case "update":
                    if (!iPlayer.HasPermission(PermAdmin))
                    {
                        iPlayer.Reply(Lang("NoUse", iPlayer.Id, command));
                        return;
                    }

                    if (args.Length < 5)
                    {
                        iPlayer.Reply(Lang("UpdateUsage", iPlayer.Id, command));
                        return;
                    }

                    var idHolderToUpdate = FindPlayerWithStateID(args[1]);

                    if (idHolderToUpdate == null)
                    {
                        iPlayer.Reply(Lang("NotFound", iPlayer.Id, command));
                        return;
                    }

                    if (storedData.StateIDs.ContainsKey(idHolderToUpdate.Id))
                    {
                        if (storedData.StateIDs[idHolderToUpdate.Id].IsEmpty())
                        {
                            iPlayer.Reply(Lang("NotFound", iPlayer.Id, command));
                            return;
                        }

                        storedData.StateIDs[idHolderToUpdate.Id].First().FirstName = args[2].ToUpper();
                        storedData.StateIDs[idHolderToUpdate.Id].First().LastName = args[3].ToUpper();
                        storedData.StateIDs[idHolderToUpdate.Id].First().HomeRealm = args[4].ToUpper();

                        iPlayer.Reply(Lang("Update_Success", iPlayer.Id, args[1]));
                    }

                    return;

                case "delete":
                    if (!iPlayer.HasPermission(PermAdmin))
                    {
                        iPlayer.Reply(Lang("NoUse", iPlayer.Id, command));
                        return;
                    }

                    if (args.Length < 2)
                    {
                        iPlayer.Reply(Lang("DeleteUsage", iPlayer.Id, command));
                        return;
                    }

                    IPlayer toDelete = FindPlayerWithStateID(args[1]);

                    if (toDelete == null || !storedData.StateIDs.ContainsKey(toDelete.Id))
                    {
                        iPlayer.Reply(Lang("NotFound", iPlayer.Id, command));
                        return;
                    }

                    storedData.StateIDs.Remove(toDelete.Id);
                    iPlayer.Reply(Lang("Delete_Success", iPlayer.Id, args[1]));

                    return;

                case "wipe":
                    if (!iPlayer.HasPermission(PermAdmin))
                    {
                        iPlayer.Reply(Lang("NoUse", iPlayer.Id, command));
                        return;
                    }

                    storedData.Clear();
                    StateID.CurrentID = 0;
                    SaveData();

                    iPlayer.Reply(Lang("Wipe_Success", iPlayer.Id, command));

                    return;
        	}
        }

        #endregion Commands

        #region Methods

        #endregion Methods

        #region API

        #endregion API

        #region Helpers

       private IPlayer GetActivePlayerByUserID(string userID)
        {
            foreach (var player in players.Connected)
                if (player.Id == userID) return player;
            return null;
        }

        public BasePlayer GetAnyPlayerByUserID(string userID)
        {
            foreach (var player in BasePlayer.allPlayerList)
                if (player.UserIDString == userID) return player;
            return null;
        }

        public IPlayer FindPlayer(string nameOrId)
        {
            foreach (var activePlayer in BasePlayer.allPlayerList)
            {
                if (activePlayer.UserIDString == nameOrId)
                    return activePlayer.IPlayer;
                if (activePlayer.displayName.ToLower() == nameOrId.ToLower())
                    return activePlayer.IPlayer;
            }

            return FindPlayerWithStateID(nameOrId);
        }

        private StateID FindIDWithNumber(string id)
        {
            var query = from outer in storedData.StateIDs
                        from inner in outer.Value
                        where inner.Number.ToString() == id
                        select inner;

            if (!query.Any()) return null;
            return query.First();
        }



        private IPlayer FindPlayerWithStateID(string id)
        {
            var query = from outer in storedData.StateIDs
                        from inner in outer.Value
                        where inner.Number.ToString() == id
                        select outer;

            if (!query.Any()) return null;
            return FindPlayer(query.First().Key);
        }

        #endregion Helpers

        #region Configuration

        private class Configuration
        {
            [JsonProperty("Future Config Options Here")]
            public bool TempBool = true;

            public string ToJson() => JsonConvert.SerializeObject(this);

            public Dictionary<string, object> ToDictionary() => JsonConvert.DeserializeObject<Dictionary<string, object>>(ToJson());
        }

        protected override void LoadDefaultConfig() => config = new Configuration();

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null)
                {
                    throw new JsonException();
                }

                if (!config.ToDictionary().Keys.SequenceEqual(Config.ToDictionary(x => x.Key, x => x.Value).Keys))
                {
                    Puts("Configuration appears to be outdated; updating and saving");
                    SaveConfig();
                }
            }
            catch
            {
                Puts($"Configuration file {Name}.json is invalid; using defaults");
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig()
        {
            Puts($"Configuration changes saved to {Name}.json");
            Config.WriteObject(config, true);
        }

        #endregion Configuration

        #region Data

        private class StoredData
        {
            public Dictionary<string, List<StateID>> StateIDs = new Dictionary<string, List<StateID>>();

            public StoredData()
            {
            }

            public void Clear()
            {
            	StateIDs.Clear();
            }
        }

        private class StateID
        {
        	public static double CurrentID = 0;

        	public double Number { get; set; }
        	public string FirstName { get; set; }
        	public string LastName { get; set; }
        	public string HomeRealm { get; set; }

        	[JsonConstructor]        	
        	public StateID(double number, string firstName, string lastName, string homeRealm)
        	{
        		Number = number;
                FirstName = firstName;
                LastName = lastName;
                HomeRealm = homeRealm;
        	}

        	public StateID(string firstName, string lastName, string homeRealm) : this(++CurrentID, firstName, lastName, homeRealm)
        	{        		
        	}

        	public Dictionary<string, object> ToDictionary() => JsonConvert.DeserializeObject<Dictionary<string, object>>(JsonConvert.SerializeObject(this));
        }

        private void SaveData() => Interface.Oxide.DataFileSystem.WriteObject(Name, storedData);

        #endregion Data
	}
}