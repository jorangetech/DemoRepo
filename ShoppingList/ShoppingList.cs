using Facepunch.Extend;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Game.Rust.Cui;
using ProtoBuf;
using Steamworks.Data;
using System.Collections.Generic;
using System.Drawing;
using UnityEngine;
using UnityEngine.PlayerLoop;
using UnityEngine.UIElements;
using VLB;
using static ConsoleSystem;

#region  ----------  TODO LIST  ----------

/**********************************************
  TODO LIST:

    BUGS:
        gathering wood - on chop of tree the gather didnt register done -- looked into, appears to be an issue with
        how the hook works?
        
    TODOs:
        Complete STATUS column to give visual representation that item is complete. Colored button on/off?
 
    DONE - remove all unnecessary debug messages
    DONE - comment where required;  confirm comments are sufficient
    DONE - wrap dev/debug messages with a conditional and use a global DEBUGMODE flag to display them; similar with ERROR msgs
            -- this is just to exist so demoing can be done without the clutter; this sort of thing would not exist for production
            except for specific scenarios.

    VERIFY DONE - add applicable toast messages
    DONE - add done flag to each item
    DONE - add done flag to list as a whole
    
    DONE --- CONFIG -- add req data; remove temp data; clean up; add button text
        review the "item string" being used, do we need to change?

    DONE --- COMMANDS -- clean/organize

    DONE --- clean up how CreateDefaultData() should work -- used to quickly a data file if main funcationality is broken

    GATHERING
        DONE - maybe add pickup-ables (wood,etc)
        DONE - see if cloth leather works; added to base collectibles in config; may remove leather to show what happens

    NEED QTY and HAVE QTY
        DONE - set done flag based on have >= need; fixed bug where flag would not flip back if player updated NEED qty
        NOT REQD - set shopping list done flag based on all done flags - not necessary at the moment
        DONE - qty updates
        DONE - edit the NEED via UI
        TODO - review setting default NEED values

    UI
        NO  --  add +100 -100 buttons; reset to zero button?    --- player can edit NEED value
        DONE -- update data?  data entry fields possible?       --- NEED qty field only
        DONE -- make the rendering of rows dynamic -- only if there is time

        TODO FUTURE -- add UI data rows to config and draw that way - tons of work

    
    AFTER DEMO
        add a way to add an item in the list?
        remove an item? or just reset the list?
        maybe if no list exists when player opens UI, add wood as default and inform to hit/gather stuff
        to have items added to list and they can adjust once in list?




**********************************************/
#endregion

namespace Oxide.Plugins {

    [Info("ShoppingList", "JAM", "0.1.0")]

    class ShoppingList : RustPlugin {

        #region  ----------  VARS  ---------- 
        
        private const string permAdmin = "ShoppingList.admin";
        private const string permUse = "ShoppingList.use";
        private const int defaultNeedQty = 10000;

        private const bool DEV_MODE = true;     //  any dev msgs will be displayed (console and chat)
        private const bool PROD_MODE = false;    //  supercedes DEV_MODE; only valid warn/error msgs displayed (console and chat)

        #endregion  //  VARS

        #region  ----------  CONFIG  ---------- 

        private ConfigData configData;

        class ConfigData {

            [JsonProperty(PropertyName = "titleText")]
            public string titleText = "Shopping List";

            [JsonProperty(PropertyName = "itemText")]
            public string itemText = "Item";

            [JsonProperty(PropertyName = "needText")]
            public string needText = "Need";

            [JsonProperty(PropertyName = "haveText")]
            public string haveText = "Have";

            [JsonProperty(PropertyName = "closeText")]
            public string closeText = "CLOSE";

            [JsonProperty(PropertyName = "resetText")]
            public string resetText = "RESET";

            [JsonProperty(PropertyName = "itemDisplayNames")]
            public Dictionary<string, string> itemDisplayNames = new Dictionary<string, string>();

            //  TODO: have a place to add items which are currently ignored for the shopping list
            //[JsonProperty(PropertyName = "itemsTBDFutureList")]
            //public Dictionary<string, string> itemsTBDFutureList = new Dictionary<string, string>();

            //  Toast messages
            [JsonProperty(PropertyName = "toastMessage01")]
            public string toastMessage01 = "Currently, there is no Shopping List data for {player.displayName}. GO HIT A TREE! Using default values for now.";

            [JsonProperty(PropertyName = "toastMessage02")]
            public string toastMessage02 = "Congrats! You have gathered the {item.needQty} {item.name} you needed.";
        }

        private bool LoadConfigVariables() {

            try {
                configData = Config.ReadObject<ConfigData>();

            } catch {
                return false;
            }
            SaveConfig(configData);
            return true;
        }

        void Init() {
            permission.RegisterPermission(permAdmin, this);
            permission.RegisterPermission(permUse, this);
            if (!LoadConfigVariables()) {
                Puts("(Init) Config file issue detected. Delete file or check syntax/fix.");
                return;
            }
        }
        
        protected override void LoadDefaultConfig() {

            configData = new ConfigData();

            //  add this data to separate section possibly

            //  create item names object - valid/included; anything else gathered will be ignored
            //  TODO:   create separate method for this

            configData.itemDisplayNames.Add("wood", "Wood");
            configData.itemDisplayNames.Add("stones", "Stone");
            configData.itemDisplayNames.Add("metal.ore", "Metal");
            configData.itemDisplayNames.Add("sulfur.ore", "Sulfur");
            configData.itemDisplayNames.Add("cloth", "Cloth");
            configData.itemDisplayNames.Add("leather", "Leather");
            configData.itemDisplayNames.Add("bearmeat", "BearMeat");
            configData.itemDisplayNames.Add("bone.fragments", "BoneFrags");
            configData.itemDisplayNames.Add("fat.animal", "AnimalFat");

            //Puts("(LoadDefaultConfig) Creating new config file");
            SaveConfig(configData);
        }

        void SaveConfig(ConfigData config) {
            Config.WriteObject(config, true);
        }
        #endregion // CONFIG

        #region  ----------  COMMANDS: CONFIG (TODO: clean up)  ---------- 

        //  -------------------------------------
        //  Chat Commands
        //  -------------------------------------

        //  METHOD: viewlist
        //  Desc:   mainly developer usage only

        [ChatCommand("viewlist")]
        void viewlist(BasePlayer player) {
            //  debug tool to check player shopping list, TODO
            SendReply(player, "(viewlist: method incomplete) Shopping list is empty");
        }

        //  METHOD: viewcfg
        //  Desc:   mainly developer usage only

        [ChatCommand("viewcfg")]
        void viewcfg(BasePlayer player) {
            //  debug tool to check cfg vals for now, TODO
            SendReply(player, $"(viewcfg: method incomplete) Config.titleText == {configData.titleText}");
        }

        //  METHOD: printcfg
        //  Desc:   mainly developer usage only

        [ChatCommand("printcfg")]
        void printcfg(BasePlayer player) {

            //  ADMIN ONLY
            if (!hasPermission(player)) {
                SendReply(player, "(printcfg) You do not have permission to use this command");
                return;
            }
            else {
                //  debug tool to check cfg vals for now, TODO
                SendReply(player, $"(printcfg: method incomplete) Config.titleText == {configData.titleText}");

                SendReply(player, "Shopping List Config");
                SendReply(player, $"titleText == {configData.titleText}");
                SendReply(player, $"needText == {configData.needText}");
                SendReply(player, $"haveText == {configData.haveText}");
                SendReply(player, $"...itemDisplayNames...");
                foreach (var item in configData.itemDisplayNames) {
                    SendReply(player, $"{item.Key} == {item.Value}");
                }
            }
        }

        //  -------------------------------------
        //  Console Commands
        //  -------------------------------------

        //  METHOD: printcfg
        //  Desc:   mainly developer usage only

        [ConsoleCommand("printcfg")]
        void printcfg(ConsoleSystem.Arg args) {

            Puts("Shopping List Config");
            Puts($"titleText == {configData.titleText}");
            Puts($"needText == {configData.needText}");
            Puts($"haveText == {configData.haveText}");
            Puts($"...itemDisplayNames...");
            foreach (var item in configData.itemDisplayNames) {
                Puts($"{item.Key} == {item.Value}");
            }
        }
        #endregion  //  COMMANDS: CONFIG

        #region  ----------  DATA  ---------- 

        StoredData storedData;

        class StoredData {

            //  CLASS:  ItemLineData
            //  Desc:   Contains all items within player's shopping list 
            public class ItemLineData {
                public string name;         //  name of item == data value of Rust's default "shortname"
                public int needQty;         //  quantity of item required
                public int haveQty;         //  quantity of item gathered
                public bool done = false;   //  flag designating if item is done or not (need == have)
            }

            //  CLASS:      ShoppingList
            //  Descrition: Shopping List - for each user with an array of items
            public class ShoppingList {
                //  List name - may be unnecessary for now
                public string? Name { get; set; }

                //  flag designating if shopping list is done or not - not used at the moment
                public bool done = false;

                //  List<ItemLineData> will be serialized as a JSON array
                public List<ItemLineData> ListofItems { get; set; } = new List<ItemLineData>();
            }

            public Dictionary<string, ShoppingList> PlayerShoppingList = new Dictionary<string, ShoppingList>();
        }

        //  METHOD: CreateDefaultData
        //  Desc:   Creates default shopping list data for given player. 
        protected void CreateDefaultData(BasePlayer player) {

            //Puts("(CreateDefaultData) Creating default data file");

            if (!storedData.PlayerShoppingList.ContainsKey(player.UserIDString)) {
                var newShopList = new StoredData.ShoppingList();
                var Item01 = new StoredData.ItemLineData();
                var Item02 = new StoredData.ItemLineData();
                var Item03 = new StoredData.ItemLineData();

                Item01.name = getItemDisplayNamesKey("Wood");
                Item01.needQty = 0;
                Item01.haveQty = 0;

                Item02.name = getItemDisplayNamesKey("Stone");
                Item02.needQty = 0;
                Item02.haveQty = 0;

                Item03.name = getItemDisplayNamesKey("Metal");
                Item03.needQty = 0;
                Item03.haveQty = 0;

                //Item04.name = getItemDisplayNamesKey("Sulfur");
                //Item04.needQty = 0;
                //Item04.haveQty = 0;

                newShopList.Name = "Default List";

                newShopList.ListofItems.Add(Item01);
                newShopList.ListofItems.Add(Item02);
                newShopList.ListofItems.Add(Item03);

                storedData.PlayerShoppingList.Add(player.UserIDString, newShopList);
            }
            SaveData();         //  possible move into IF clause
        }

        //  METHOD: getItemDisplayNamesKey
        //  Desc:   "reverse-lookup" on itemDisplayNames object; retrieves the KEY value based on the VALUE instead
        string getItemDisplayNamesKey(string displayName) {
            foreach (var item in configData.itemDisplayNames) {
                if (item.Value.Equals(displayName)) {
                    return item.Key;
                }
            }
            return "";
        }

        void Loaded() {
            storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>("ShoppingList");
            Interface.Oxide.DataFileSystem.WriteObject("ShoppingList", storedData);
        }
        void SaveData() {
            Interface.Oxide.DataFileSystem.WriteObject("ShoppingList", storedData);
        }

        void Unload() {
            foreach (BasePlayer current in BasePlayer.activePlayerList) {
                CuiHelper.DestroyUi(current, "HUDSLM");
            }
        }

        private void OnNewSave(string filename) {
            storedData.PlayerShoppingList.Clear();
            SaveData();
            Puts("(OnNewSave) Wipe detected. All Shopping List data has been cleared");
        }
        #endregion  //  DATA

        #region ----------  COMMANDS: DATA  ----------

        //  -------------------------------------
        //  Console Commands
        //  -------------------------------------

        //  METHOD: createDefaultData
        //  Desc:   used to create default data (shopping list w/ items) for player; shortcut to not start gathering to show the UI

        [ConsoleCommand("createDefaultData")]
        void createDefaultData(ConsoleSystem.Arg args) {
            //  TODO: add permission check and test

            if (args.Player() == null) return;

            CreateDefaultData(args.Player());
        }

        [ConsoleCommand("resetData")]
        void resetData(ConsoleSystem.Arg args) {
            //  TODO: add permission check and test

            if (args.Player() == null) return;

            storedData.PlayerShoppingList.Clear();
            SaveData();
            Puts("(resetData) Simulating a wipe. All Shopping List data has been cleared. Use createDefaultData if required.");
        }
        #endregion  //  COMMANDS: DATA

        #region  ----------  USER INTERFACE  ----------

        //  METHOD: showUI
        //  Desc:   creates and displays the player's UI
        void showUI(BasePlayer player) {

            //Puts("(showUI) creating UI");

            //  destroy the UI if it currently exists; shouldn't happen but done just in case
            CuiHelper.DestroyUi(player, "HUDSLM");

            //  check if player is valid or has permission; if not, return and UI displayed
            //      TODO: add permissions to UI usage for regular players
            if (!IsValid(player) || !hasPermission(player)) {
                return;
            };

            var container = new CuiElementContainer();

            //  close button text
            string closeText = configData.closeText;

            //  some colors
            string colorMedGray = "0.26 0.26 0.26 1";
            string colorDarkGray = "0.15 0.15 0.15 1";
            string colorLightGray = "0.5 0.5 0.5 0.5";
            string colorDarkGreen = "0 0.5 0 1";

            //  text colors
            string textColorWhite = "1 1 1 1",      //  base text color of most
                textColorYellow = "1 1 0.6 1",      //  NEEDqty
                textColorGreen = "0 1 0 1",         //  HAVEqty when < NEEDqty
                textColorBlue = "0 0 1 1",          //  TODO
                textColorOrange = "0.6 0.3 0 1";    //  HAVEqty when >= NEEDqty

            //  element background colors
            string panelColor = colorDarkGray;
            string buttonColor = colorLightGray;
            string headerColor = colorDarkGreen;

            string textColor = textColorWhite;

            string textColorItem = textColorWhite, textColorNeed = textColorYellow,
                textColorHave = textColorWhite, textColorStatus = textColorWhite;

            //  ---  MAIN PANEL  --------------------------------------------------

            var mainPanel = container.Add(new CuiPanel {
                Image = { Color = $"{panelColor}" },
                RectTransform = { AnchorMin = "0.03 0.15", AnchorMax = "0.3 0.95" },
                CursorEnabled = true
            }, "Overlay", "HUDSLM");

            //  TEMPORARY DATA ELEMENT for testing
            container.Add(new CuiElement {
                Parent = mainPanel,
                Name = "val_tempfield",
                Components = {
                    new CuiInputFieldComponent {
                        Color = $"{textColorGreen}",
                        FontSize = 14,
                        Text = "TODO",
                        CharsLimit = 11,
                        NeedsKeyboard = true,
                        Command = "chat.say"    // custom console command
                    },
                    new CuiRectTransformComponent { AnchorMin = "0.712 0.80", AnchorMax = "0.842 0.83" },
                    //new CuiOutlineComponent { Color = "0 0 0 1", Distance = "1 -1" }
                    //new CuiImageComponent { Color = "0.1 0.1 0.1 0.8" }, // Background  BREAKS RUST
                    //new CuiImageComponent { Color = "0.30 0.30 0.30 1.0", FadeIn = 0.1f, Material = "assets/content/ui/uibackgroundblur.mat" },
                }
            });

            //  ---  HEADER  --------------------------------------------------

            //  TITLE label -- data "player's display name" + sub-title text appended
            container.Add(new CuiLabel {
                Text = { Text = $"{player.displayName}'s {configData.titleText}", Color = $"{textColor}" },
                RectTransform = { AnchorMin = "0.15 0.82", AnchorMax = "0.6 0.92" }
            }, "HUDSLM");

            //  ---  SUB-HDR  -------------------------------------------------

            //  ITEM label
            container.Add(new CuiLabel {
                Text = { Text = $"{configData.itemText}", Color = $"{headerColor}" },
                RectTransform = { AnchorMin = "0.08 0.73", AnchorMax = "0.28 0.83" }
            }, "HUDSLM");

            //  NEED label
            container.Add(new CuiLabel {
                Text = { Text = $"{configData.needText}", Color = $"{headerColor}" },
                RectTransform = { AnchorMin = "0.3 0.73", AnchorMax = "0.45 0.83" }
            }, "HUDSLM");

            //  HAVE label
            container.Add(new CuiLabel {
                Text = { Text = $"{configData.haveText}", Color = $"{headerColor}" },
                RectTransform = { AnchorMin = "0.5 0.73", AnchorMax = "0.65 0.83" }
            }, "HUDSLM");
            //  ---------------------------------------------------------------


            //  ---  ITEM LIST  -----------------------------------------------

            //  if no shopping list exists for the player, display error message; for now default used to display something, but not valid/nor saved
            //      TODO: add usage of default data and create the data and read the data

            if (!storedData.PlayerShoppingList.ContainsKey(player.UserIDString)) {
                //  there is no player data, use defaults (DISPLAY ONLY - will not be saved)
                var messageText = configData.toastMessage01;
                messageText = messageText.Replace($"{{player.displayName}}", player.displayName);
                player.SendConsoleCommand("gametip.showtoast", 1, $"{messageText}");
            
            } else {

                //  Base values for AnchorMin/Max; defined as strings and whendynamically incrementing convert to number and back to string

                string baseItemAnchorMinX = "0.08";
                string baseItemAnchorMinY = "0.66";
                string baseNeedAnchorMinX = "0.3";
                string baseNeedAnchorMinY = "0.73";
                string baseHaveAnchorMinX = "0.5";
                string baseHaveAnchorMinY = "0.66";

                string baseItemAnchorMaxX = "0.28";     //+0.20
                string baseItemAnchorMaxY = "0.76";     //+0.10
                string baseNeedAnchorMaxX = "0.45";     //+0.15
                string baseNeedAnchorMaxY = "0.76";     //+0.03
                string baseHaveAnchorMaxX = "0.65";     //+0.15
                string baseHaveAnchorMaxY = "0.76";     //+0.10

                //  TODO:   determine a horizontal offset to clean this up?
                //  TODO:   renamed "base" values to generic "column01/02/03...." ?

                double verticalOffsetVal = 0.07;

                var listofitems = storedData.PlayerShoppingList[player.UserIDString].ListofItems;
                var counter = 0;
                foreach (var item in listofitems) {
                    counter++;

                    //Puts($"(showUI) [DEVMSG] in foreach; {counter} / {item.name}");

                    string curItemAnchorMinX = baseItemAnchorMinX;
                    string curItemAnchorMinY = (baseItemAnchorMinY.ToFloat() - ((counter - 1) * verticalOffsetVal)).ToString();
                    string curNeedAnchorMinX = baseNeedAnchorMinX;
                    string curNeedAnchorMinY = (baseNeedAnchorMinY.ToFloat() - ((counter - 1) * verticalOffsetVal)).ToString();
                    string curHaveAnchorMinX = baseHaveAnchorMinX;
                    string curHaveAnchorMinY = (baseHaveAnchorMinY.ToFloat() - ((counter - 1) * verticalOffsetVal)).ToString();

                    string curItemAnchorMaxX = baseItemAnchorMaxX;
                    string curItemAnchorMaxY = (baseItemAnchorMaxY.ToFloat() - ((counter - 1) * verticalOffsetVal)).ToString();
                    string curNeedAnchorMaxX = baseNeedAnchorMaxX;
                    string curNeedAnchorMaxY = (baseNeedAnchorMaxY.ToFloat() - ((counter - 1) * verticalOffsetVal)).ToString();
                    string curHaveAnchorMaxX = baseHaveAnchorMaxX;
                    string curHaveAnchorMaxY = (baseHaveAnchorMaxY.ToFloat() - ((counter - 1) * verticalOffsetVal)).ToString();

                    //  ITEM data value
                    container.Add(new CuiLabel {
                        Text = { Text = $"{configData.itemDisplayNames[item.name]}", Color = $"{textColorItem}" },
                        //  AnchorMin/Max values will need to be dynamic for each element as per column/row
                        RectTransform = { AnchorMin = $"{curItemAnchorMinX} {curItemAnchorMinY}", AnchorMax = $"{curItemAnchorMaxX} {curItemAnchorMaxY}" }
                    }, "HUDSLM", $"lbl_item0{counter}");

                    //  NEED data value
                    container.Add(new CuiElement {
                        Parent = mainPanel,
                        Name = $"val_need0{counter}",
                        Components = {
                            new CuiInputFieldComponent {
                                Color = $"{textColorNeed}",
                                FontSize = 14,
                                Text = item.needQty.ToString(),
                                CharsLimit = 6,
                                NeedsKeyboard = true,
                                // custom console command which will check and update the NEED qty for given item
                                //      args == <item name> <prev needQty>
                                //              <current needQty> (auto-default as first arg if no args sent, otherwise pushed to end)
                                Command = $"updateNeedQty {configData.itemDisplayNames[item.name]} {item.needQty.ToString()}" // (3rd arg == current needQty value)
                            },
                            //  AnchorMin/Max values will need to be dynamic for each element as per column/row
                            new CuiRectTransformComponent { AnchorMin = $"{curNeedAnchorMinX} {curNeedAnchorMinY}", AnchorMax = $"{curNeedAnchorMaxX} {curNeedAnchorMaxY}" }
                        }
                    });

                    //  set color of haveQty based on done status
                    textColorHave = item.done ? textColorOrange : textColorGreen;

                    //  HAVE data value
                    container.Add(new CuiLabel {
                        Text = { Text = item.haveQty.ToString(), Color = $"{textColorHave}" },
                        //  AnchorMin/Max values will need to be dynamic for each element as per column/row
                        RectTransform = { AnchorMin = $"{curHaveAnchorMinX} {curHaveAnchorMinY}", AnchorMax = $"{curHaveAnchorMaxX} {curHaveAnchorMaxY}" }
                    }, "HUDSLM", $"val_have0{counter}");
                }
            }

            //  ---  FOOTER  --------------------------------------------------

            //  CLOSE button
            container.Add(new CuiButton {
                Button = { Command = "closeui", Color = $"{buttonColor}" },
                RectTransform = { AnchorMin = "0.75 0.89", AnchorMax = "0.9 0.92" },
                Text = { Text = $"{closeText}", Color = $"{textColor}", Align = TextAnchor.MiddleCenter }
            }, "HUDSLM");

            //  create the UI for player
            CuiHelper.AddUi(player, container);
        }
        #endregion  ----------  USER INTERFACE  ----------

        #region  ----------  COMMANDS: UI & OTHER  ----------

        //  -------------------------------------
        //  ChatCommands
        //  -------------------------------------

        //  METHOD: callui
        //  Desc:   used to call the UI by the player

        [ChatCommand("callui")]
        void callui(BasePlayer player) {
            showUI(player);
        }

        //  -------------------------------------
        //  Console Commands
        //  -------------------------------------

        //  METHOD: closeui
        //  Desc:   used by UI Close button

        [ConsoleCommand("closeui")]
        void closeui(ConsoleSystem.Arg args) {

           if (args.Player() == null) return;

            CuiHelper.DestroyUi(args.Player(), "HUDSLM");
            //Puts("(closeui) UI closed");
        }

        //  METHOD: updateNeedQty
        //  Desc:   used by UI when player wants to update the NEED qty

        [ConsoleCommand("updateNeedQty")]
        void updateNeedQty(ConsoleSystem.Arg args) {

            //  TODO: cleanup/reorg; look into the error where args are missing - I think it is when needqty is null

            if (args.Player() == null) return;

            var argsStr = "";
            for (int i = 0; i < args.Args.Length; i++) {
                argsStr = argsStr + ";" + args.Args[i];
            }

            if (isDevMode()) Puts($"(updateNeedQty) [DEVMSG] arguments received: {argsStr}");

            if (!args.Args.Length.Equals(3)) {
                Puts($"(updateNeedQty) [DEV.ERROR]: expected 3 args, only got {args.Args.Length} == {argsStr}");
                return;
            }

            //--------------------------------

            string itemDisplayName = args.Args[0];
            string prevQty = args.Args[1];
            int newQty = args.Args[2].ToInt();
            string itemShortName = getItemDisplayNamesKey(itemDisplayName);

            bool updated = false;
            if (!args.Args[2].Equals(args.Args[1])) {
                updated = SetItemLine(args.Player(), itemShortName, newQty);
            }

            if (updated) SaveData();

            if (isDevMode()) Puts($"(updateNeedQty) [DEVMSG] Item {itemShortName} previous qty was {prevQty}; it is now {newQty.ToString()}; UPDATED: {updated}");

        }

        //  TODO: TBD keep or not
        [ConsoleCommand("resetItem")]
        void resetItem(ConsoleSystem.Arg args) {

            if (args.Player() == null) return;

            //Puts("(resetItem) [DEVMSG] this function does nothing for now");                
        }
        #endregion  //  COMMANDS: UI & OTHER

        #region  ----------  HOOKS & THINGS  ----------

        //  HOOK:   OnDispenserGather
        //  Desc:   TODO - does a bunch of stuff
        private void OnDispenserGather(ResourceDispenser dispenser, BasePlayer player, Item item) {

            if (item == null || dispenser == null || !IsValid(player) || !hasPermission(player)) {
                Puts("(OnDispenserGather) ERROR: null or invalid/perms issue");
                return;
            }

            if (isDevMode()) Puts($"(OnDispenserGather) [DEVMSG] gathered {item.amount} {item.info.shortname}");

            //  TODO:  check if the item being gathered is in the current "valid" list, if not ignore
            //          but add the item to the "future items" list (in config file?)
            bool foundIt = false;
            foreach (var z in configData.itemDisplayNames) {
                if (item.info.shortname.Equals(z.Key)) {
                    foundIt = true;
                    break;
                }
            }

            //  the item was not found in the preset items, ignore the item (for now)
            if (!foundIt) {
                if (isDevMode()) Puts($"(OnDispenserGather) [DEVMSG] ITEM NOT FOUND -- SKIPPING gathered {item.amount} {item.info.shortname}");
                return;
            }

            //  now check current inventory and adjust the amount collected and save the data object
            validateInventoryAmountsAndUpdateData(player, item.info.shortname, item.amount);
        }

        //  HOOK:   OnCollectiblePickup
        //  Desc:   TODO - does a bunch of stuff
        private void OnCollectiblePickup(CollectibleEntity collectible, BasePlayer player) {

            if (collectible == null || collectible.itemList.IsNullOrEmpty() || !IsValid(player) || !hasPermission(player))
                return;

            //Puts($"(OnCollectiblePickup) collectible.itemList: {collectible.itemList} collectible.itemName: {collectible.itemName}");

            bool foundIt = false;
            ItemAmount itemAmount;
            string itemFoundName = "";
            int itemFoundAmount = 0;

            for (int i = 0; i < collectible.itemList.Length; i++) {
                itemAmount = collectible.itemList[i];
                if (itemAmount == null) continue;
                if (itemAmount.itemDef == null) continue;

                if (isDevMode()) Puts($"(OnCollectiblePickup) [DEVMSG] item collected: {itemAmount.itemDef.shortname} amount: {itemAmount.amount}");

                //  check and see if the item is a valid shopping list item
                foreach (var validShoppingListItem in configData.itemDisplayNames) {
                    if (itemAmount.itemDef.shortname.Equals(validShoppingListItem.Key)) {
                        foundIt = true;
                        itemFoundName = itemAmount.itemDef.shortname;
                        itemFoundAmount = (int) itemAmount.amount;
                        if (isDevMode()) {
                            Puts($"(OnCollectiblePickup) [DEVMSG] itemShortName: {itemFoundName} and amount: {itemFoundAmount.ToString()}");
                            Puts($"(OnCollectiblePickup) [DEVMSG] any other associated collectibles (seeds, etc) will not be tallied at this time. (future enhancement)");
                        }
                        break;
                    }
                }
            }

            //  the item was not found in the validShoppingList items, ignore the item (for now)
            if (!foundIt) {
                if (isDevMode()) Puts($"(OnCollectiblePickup) [DEVMSG] ITEM NOT FOUND. Skipping all items collected. (future enhancement)");
                return;
            }
            //

            var itemName = itemFoundName;

            //  now check current inventory and adjust the amount collected and save the data object
            validateInventoryAmountsAndUpdateData(player, itemFoundName, itemFoundAmount);
        }
        #endregion

        #region  ----------  HELPERS  ----------

        //  METHOD: hasPermission
        //  Desc:   check if player is valid
        private bool IsValid(BasePlayer player) {
            return player != null && player.userID.IsSteamId();
        }

        //  METHOD: hasPermission
        //  Desc:   check if player has permission to use plugin - modify to have admin/default perms
        private bool hasPermission(BasePlayer player) {
            return permission.UserHasPermission(player.userID.ToString(), permAdmin);
        }

        //  METHOD: isDevMode
        //  Desc:   based on dev/prod mode flags
        private bool isDevMode() {
            //  TODO:   add backup check that confirms if on DEV server or PROD server to alleviate unwanted consequences
            //          also, after all is tested and complete, devmsgs should be removed anyways
            if (PROD_MODE) {
                return false;
            } else {
                return DEV_MODE;
            }
        }

        //  METHOD: validateInventoryAmountsAndUpdateData
        //  Desc:   after gathering/collecting, add the amount gathered/collected to the inventory's item quantity (if it exists)
        //          and update the shopping list data for given player. 
        private void validateInventoryAmountsAndUpdateData(BasePlayer player, string itemName, int itemAmount) {

            //Puts($"(validateInventoryAmountsAndUpdateData) INV.itemList == {player.inventory.containerMain.itemList} ");
            //Puts($"(validateInventoryAmountsAndUpdateData) INV.count == {player.inventory.containerMain.itemList.Count} ");

            bool foundItem = false;
            foreach (var x in player.inventory.containerMain.itemList) {
                //Puts($"(validateInventoryAmountsAndUpdateData) text: {x.text};" +  <<< empty
                //    $" name: {x.name};" +                     <<< empty
                //    $" amount: {x.amount};" +                 amount prior to gather
                //    $" info: {x.info}" +                      "wood.item (ItemDefinition)"
                //    $" info.shortname: {x.info.shortname}");  "wood"

                if (x.info.shortname.Equals(itemName)) {
                    foundItem = true;
                    var newAmount = itemAmount + x.amount;
                    if (isDevMode()) {
                        Puts($"(validateInventoryAmountsAndUpdateData) [DEVMSG] you have {x.amount} {x.info.shortname} prior to gather");
                        Puts($"(validateInventoryAmountsAndUpdateData) [DEVMSG] {itemName} current: {x.amount}; gathered: {itemAmount}; newAmt: {newAmount}");
                    }
                    SaveItemData(player, itemName, newAmount);
                    break;
                }
            }

            if (!foundItem) {
                //  first time gathering, add new item
                var newAmount = itemAmount;
                if (isDevMode()) {
                    Puts($"(validateInventoryAmountsAndUpdateData) [DEVMSG] FIRST GATHER -- zero {itemName} prior to gather");
                    Puts($"(validateInventoryAmountsAndUpdateData) [DEVMSG] {itemName} current: 0; gathered: {itemAmount}; newAmt: {newAmount}");
                }
                SaveItemData(player, itemName, newAmount);
            }
        }

        //  METHOD: GetItemLine
        //  Desc:   retrieve line items from stored shopping list; used by UI for now
        StoredData.ItemLineData GetItemLine(BasePlayer player, string itemname) {
            if (!storedData.PlayerShoppingList.ContainsKey(player.UserIDString)) {

                return null;

            } else {

                var listofitems = storedData.PlayerShoppingList[player.UserIDString].ListofItems;
                foreach (var item in listofitems) {
                    if (item.name.Equals(itemname)) {
                        return item;
                    }
                }
                return null;
            }
        }

        //  METHOD: SetItemLine
        //  Desc:   set given line item NEED QTY - used by UI when changing the needQty
        //          TODO - change return value once confirmed
        //TODO:  StoredData.ItemLineData SetItemLine(BasePlayer player, string itemname, int newNeedQty)
        bool SetItemLine(BasePlayer player, string itemname, int newNeedQty) {

            if (!storedData.PlayerShoppingList.ContainsKey(player.UserIDString)) {

                Puts($"(SetItemLine) [WARNMSG] player {player.UserIDString} does not exist");
                return false;

            } else {

                if (isDevMode()) Puts($"(SetItemLine) [DEVMSG] player {player.UserIDString} exists...");
                var listofitems = storedData.PlayerShoppingList[player.UserIDString].ListofItems;

                foreach (var item in listofitems) {
                    if (isDevMode()) Puts($"(SetItemLine) [DEVMSG] itemname.to.find {itemname} ** item.in.list {item.name}");
                    if (item.name.Equals(itemname)) {
                        if (isDevMode()) Puts($"(SetItemLine) [DEVMSG] itemname.to.find {itemname} == item.in.list {item.name}; item.needQty (cur={item.needQty}) to be updated {newNeedQty}");
                        item.needQty = newNeedQty;
                        return true;
                    }
                }
                if (isDevMode()) Puts($"(SetItemLine) [DEVMSG] appears the itemname.to.find {itemname} does not exist in {player.UserIDString}'s shopping list");
                return false;
            }
        }

        //  METHOD: isItemDone
        //  Desc:   check if player has gathered the quantity they needed; returns true or false and used to set the "done" flag
        bool isItemDone(BasePlayer player, StoredData.ItemLineData item) {

            bool status = item.done;

            //  display toast message when player has collected what they needed - check if done so not to display repeatedly
            if (!status && item.haveQty >= item.needQty) {

                var messageText = configData.toastMessage02;
                messageText = messageText.Replace($"{{item.needQty}}", item.needQty.ToString());
                messageText = messageText.Replace($"{{item.name}}", item.name);

                if (isDevMode()) Puts($"[DEVMSG] {messageText}");
                player.SendConsoleCommand("gametip.showtoast", 2, $"{messageText}");

                status = true;
            } else {
                //  reset the item's done status == false if HAVE < NEED (player adjusts NEED qty via UI)
                if (status && item.haveQty < item.needQty) {
                    status = false;
                }
            }
            return status;
        }

        //  METHOD: SaveItemData
        //  Desc:   Assuming a valid player, check to see if a current ShoppingList exists; if not, create new ShoppingList with new item.
        //          Otherwise, check if current ShoppingList contains the item and update the HAVE qty, if not, create a new item.
        void SaveItemData(BasePlayer player, string itemname, int amount) {

            if (IsValid(player)) {
                if (isDevMode()) Puts($"(SaveItemData) [DEVMSG] gathered {amount} of {itemname}");

                //  TODO rename this something like newLineItem
                StoredData.ItemLineData itemLineData = new StoredData.ItemLineData();
                itemLineData.name = itemname;
                itemLineData.haveQty = amount;
                if (itemLineData.needQty <= 0) itemLineData.needQty = defaultNeedQty;

                //  check if the player already has a working shopping list,
                //  if not add a new player record and the item
                if (!storedData.PlayerShoppingList.ContainsKey(player.UserIDString)) {
                    var newShopList = new StoredData.ShoppingList();
                    newShopList.Name = "Auto Created List";

                    //  needQty won't exist so just default it to 10000
                    itemLineData.needQty = defaultNeedQty;

                    //  check if item is DONE (haveQty >= needQty)
                    itemLineData.done = isItemDone(player, itemLineData);

                    newShopList.ListofItems.Add(itemLineData);
                    storedData.PlayerShoppingList.Add(player.UserIDString, newShopList);

                    //SendReply(player, $"(SaveItemData) NEW PLAYER RECORD - added new {itemname} item");
                    if (isDevMode()) Puts($"(SaveItemData) [DEVMSG] NEW PLAYER RECORD(2) - added new {itemname} item");

                    SaveData();
                    return;
                } else {
                    //  otherwise, check if the given item already exists
                    //      if so, update the record; otherwise, add new item

                    //SendReply(player, $"(SaveItemData) Player exists, find {itemname} item record first");
                    if (isDevMode()) Puts($"(SaveItemData) [DEVMSG] Player exists, find {itemname} item record first");

                    var listofitems = storedData.PlayerShoppingList[player.UserIDString].ListofItems;
                    foreach (var item in listofitems) {
                        if (item.name.Equals(itemname)) {
                            //  then just update haveQty
                            item.haveQty = amount;
                            if (item.needQty <= 0) item.needQty = defaultNeedQty;

                            if (isDevMode()) {
                                Puts($"(SaveItemData) [DEVMSG] FOUND {itemname} item; updating haveQty = {amount}");
                                Puts($"(SaveItemData) [DEVMSG] {itemname} have {item.haveQty} *** need {item.needQty}");
                            }
                            //  check if item is DONE (haveQty >= needQty)
                            item.done = isItemDone(player, item);

                            SaveData();
                            return;
                        }
                    }

                    //  if we are here, item was not found, so add new item

                    //  check and compare haveQty to needQty; set done flag accordingly

                    if (isDevMode()) {
                        Puts($"(SaveItemData) [DEVMSG] {itemname} item NOT FOUND; adding new {itemname} with haveQty = {amount}");
                        Puts($"(SaveItemData) [DEVMSG] {itemname} have {itemLineData.haveQty} *** need {itemLineData.needQty}");
                    }

                    //  check if item is DONE (haveQty >= needQty)
                    itemLineData.done = isItemDone(player, itemLineData);

                    storedData.PlayerShoppingList[player.UserIDString].ListofItems.Add(itemLineData);
                    SaveData();
                    return;
                }
                //SaveData();
                //return;
            } else {
                return;
            }
        }
        #endregion  //  HELPERS

    }
}
