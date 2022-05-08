using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using R2API.Utils;
using RoR2;
using RoR2.Networking;
using UnityEngine;
using UnityEngine.Networking;
using Zio.FileSystems;

namespace ItemLimited
{
    [BepInDependency("com.bepis.r2api")]
    [BepInPlugin(GUID, MOD_NAME, MOD_VERSION)]
    [NetworkCompatibility(CompatibilityLevel.NoNeedForSync, VersionStrictness.DifferentModVersionsAreOk)]
    [R2API.Utils.R2APISubmoduleDependency(nameof(CommandHelper))]
    public class ItemLimited : BaseUnityPlugin
    {
        public const string GUID = "com.Lunzir.ItemLimited", MOD_NAME = "ItemLimited", MOD_VERSION = "1.1.6";

        public static PluginInfo PluginInfo;
        public string LIMIT_ITEM_TO_SCRAP => Language.GetString("LIMIT_ITEM_TO_SCRAP");
        public string LIMIT_ITEM_PROHIBITED => Language.GetString("LIMIT_ITEM_PROHIBITED");
        public string LIMIT_ITEM_REACH_MAX => Language.GetString("LIMIT_ITEM_REACH_MAX");

        //Dictionary<NetworkIdentity, PlayerStruct> PlayerStructs = new Dictionary<NetworkIdentity, PlayerStruct>();
        /// <summary>
        /// 三种交互获得物品行为，1、直接送包里。2、按E交互。3、身体走过去触发交互。
        /// </summary>
        public void Awake()
        {
            ModConfig.InitConfig(Config);
            if (ModConfig.EnableMod.Value)
            {
                PluginInfo = Info;
                Tokens.RegisterLanguageTokens();

                On.RoR2.Run.Start += Run_Start;
                // 地上交互
                //On.RoR2.GenericPickupController.AttemptGrant += GenericPickupController_AttemptGrant;
                //On.RoR2.EquipmentDef.AttemptGrant += EquipmentDef_AttemptGrant;
                // 包里判断
                On.RoR2.CharacterMaster.OnInventoryChanged += CharacterMaster_OnInventoryChanged;
                On.RoR2.Inventory.SetEquipmentIndex += Inventory_SetEquipmentIndex;
                R2API.Utils.CommandHelper.AddToConsoleWhenReady();
            }
        }


        private void Run_Start(On.RoR2.Run.orig_Start orig, Run self)
        {
            orig(self);

            Config.Reload();
            ModConfig.InitConfig(Config);

            StartCoroutine(InitItem());
            StartCoroutine(InitEquipment());
        }

        private void GenericPickupController_AttemptGrant(On.RoR2.GenericPickupController.orig_AttemptGrant orig, GenericPickupController self, CharacterBody body)
        {
            if (!NetworkServer.active)
            {
                return;
            }
            TeamComponent component = body.GetComponent<TeamComponent>();
            if (component && component.teamIndex == TeamIndex.Player)
            {
                //Send("Into GenericPickupController_AttemptGrant");
                bool giveFlag = true;
                PickupDef pickupDef = PickupCatalog.GetPickupDef(self.pickupIndex);

                // 共享
                ItemDef itemDef = ItemCatalog.GetItemDef(pickupDef.itemIndex);
                if (itemDef)
                {
                    if (ItemStruct.Instance.TryGetValue(itemDef.name.ToLower(), out ItemStruct itemStruct)) // 找当前物品
                    {
                        Inventory inventory = body.inventory;
                        ItemIndex itemIndex = pickupDef.itemIndex;

                        int currnetCount = inventory.GetItemCount(itemIndex); // 包里物品数量
                        int max = itemStruct.PickupMax;
                        if (currnetCount >= max && max != -1) // 当前物品大于设定值
                        {
                            giveFlag = LimitHandler(body, itemStruct, inventory, max, false);
                            //Send($"{itemDef.name}({Language.GetString(itemDef.nameToken)}({Language.GetString(itemDef.pickupToken)}))");
                        }
                    }
                }

                EquipmentDef equipmentDef = EquipmentCatalog.GetEquipmentDef(pickupDef.equipmentIndex);
                if (equipmentDef)
                {
                    if (EquipmentStruct.Instance.TryGetValue(equipmentDef.name.ToLower(), out EquipmentStruct equipmentStruct))
                    {
                        if (equipmentStruct.Enable != true)
                        {
                            string equipName = Language.GetString(EquipmentCatalog.GetEquipmentDef(equipmentDef.equipmentIndex).nameToken);
                            Send($"<style=cIsHealth>{equipName}</style>{Language.GetString(LIMIT_ITEM_PROHIBITED)}", body.networkIdentity.clientAuthorityOwner);
                            PickupDropletController.CreatePickupDroplet(self.pickupIndex, body.corePosition + Vector3.up * 2f, Vector3.up * 10f);
                            giveFlag = false;
                        }
                        //Send($"{equipmentDef.name}({Language.GetString(equipmentDef.nameToken)}({Language.GetString(equipmentDef.pickupToken)}))");
                    }
                }
                if (giveFlag)
                {
                    if (body.inventory && pickupDef != null)
                    {
                        PickupDef.GrantContext grantContext = new PickupDef.GrantContext
                        {
                            body = body,
                            controller = self
                        };
                        PickupDef.AttemptGrantDelegate attemptGrant = pickupDef.attemptGrant;
                        if (attemptGrant != null)
                        {
                            attemptGrant(ref grantContext); // 给物品
                        }
                        if (grantContext.shouldNotify)
                        {
                            GenericPickupController.SendPickupMessage(body.master, pickupDef.pickupIndex);
                        }
                        if (grantContext.shouldDestroy)
                        {
                            self.consumed = true;
                            UnityEngine.Object.Destroy(self.gameObject);
                        }
                    }
                }
                else
                {
                    UnityEngine.Object.Destroy(self.gameObject);
                }

            }
        }
        private void CharacterMaster_OnInventoryChanged(On.RoR2.CharacterMaster.orig_OnInventoryChanged orig, CharacterMaster self)
        {
            if (ModConfig.EnableMod.Value)
            {
                if (self && self.teamIndex == TeamIndex.Player)
                {
                    //Send("Into CharacterMaster_OnInventoryChanged");
                    CharacterBody body = self.GetBody();
                    if (body)
                    {
                        foreach (ItemStruct item in ItemStruct.Instance.Values)
                        {
                            Inventory inventory = self.inventory;
                            int currnetCount = inventory.GetItemCount(item.ItemIndex);
                            int max = item.PickupMax;
                            if (currnetCount > max && max != -1) // 当前物品大于设定值
                            {
                                LimitHandler(body, item, inventory, max, true);
                                return;
                            }
                        }
                    }
                }
            }
            orig.Invoke(self);
        }
        private bool LimitHandler(CharacterBody body, ItemStruct item, Inventory inventory, int max, bool clean)
        {
            ItemDef itemDef = ItemCatalog.GetItemDef(item.ItemIndex);
            if(clean) inventory.GiveItem(item.ItemIndex, -1);
            bool canScrap = true;
            if (ModConfig.EnableAutoScrap.Value)
            {
                switch (itemDef.tier)
                {
                    case ItemTier.Tier1:
                        inventory.GiveItem(RoR2Content.Items.ScrapWhite, 1);
                        break;
                    case ItemTier.Tier2:
                        inventory.GiveItem(RoR2Content.Items.ScrapGreen, 1);
                        break;
                    case ItemTier.Tier3:
                        inventory.GiveItem(RoR2Content.Items.ScrapRed, 1);
                        break;
                    case ItemTier.Boss:
                        inventory.GiveItem(RoR2Content.Items.ScrapYellow, 1);
                        break;
                    default:
                        canScrap = false;
                        break;
                }
            }
            if (!ModConfig.EnableAutoScrap.Value || !canScrap) // 如果是蓝装或紫装
            {
                PickupDropletController.CreatePickupDroplet(PickupCatalog.FindPickupIndex(item.ItemIndex),
                                        body.corePosition + Vector3.up * 2f, Vector3.up * 10f);
            }
            NetworkIdentity networkIdentity = body.networkIdentity;
            if (networkIdentity)
            {
                string itemName = Language.GetString(itemDef.nameToken);
                if (ModConfig.EnableAutoScrap.Value && canScrap)
                    Send($"<style={ItemTierColor(itemDef.tier)}>{itemName}</style>{LIMIT_ITEM_TO_SCRAP}", networkIdentity.clientAuthorityOwner);
                else
                {
                    if (max == 0)
                        Send($"<style={ItemTierColor(itemDef.tier)}>{itemName}</style>{LIMIT_ITEM_PROHIBITED}", networkIdentity.clientAuthorityOwner);
                    else
                        Send($"<style={ItemTierColor(itemDef.tier)}>{itemName}</style>{LIMIT_ITEM_REACH_MAX}({max})", networkIdentity.clientAuthorityOwner);
                }
            }
            return false;
        }

        //private void EquipmentDef_AttemptGrant(On.RoR2.EquipmentDef.orig_AttemptGrant orig, ref PickupDef.GrantContext context)
        //{
        //    Send("Into EquipmentDef_AttemptGrant");
        //    context.controller.StartWaitTime();
        //    Inventory inventory = context.body.inventory;
        //    EquipmentIndex currentEquipmentIndex = inventory.currentEquipmentIndex;
        //    PickupDef pickupDef = PickupCatalog.GetPickupDef(context.controller.pickupIndex);
        //    EquipmentIndex equipmentIndex = (pickupDef != null) ? pickupDef.equipmentIndex : EquipmentIndex.None;

        //    TeamComponent component = context.body.GetComponent<TeamComponent>();
        //    if (component && component.teamIndex == TeamIndex.Player)
        //    {
        //        if (EquipmentStruct.Instance.TryGetValue(EquipmentCatalog.GetEquipmentDef(equipmentIndex).name.ToLower(), out EquipmentStruct equipmentStruct))
        //        {
        //            if (!equipmentStruct.Enable)
        //            {
        //                string equipName = Language.GetString(EquipmentCatalog.GetEquipmentDef(equipmentIndex).nameToken);
        //                Send($"<style=cIsHealth>{equipName}</style>{Language.GetString(LIMIT_ITEM_PROHIBITED)}", context.body.networkIdentity.clientAuthorityOwner);
        //                return;
        //            }
        //        } 
        //    }

        //    inventory.SetEquipmentIndex(equipmentIndex);
        //    context.controller.NetworkpickupIndex = PickupCatalog.FindPickupIndex(currentEquipmentIndex);
        //    context.shouldDestroy = false;
        //    context.shouldNotify = true;
        //    if (context.controller.pickupIndex == PickupIndex.none)
        //    {
        //        context.shouldDestroy = true;
        //    }
        //    if (context.controller.selfDestructIfPickupIndexIsNotIdeal && context.controller.pickupIndex != PickupCatalog.FindPickupIndex(context.controller.idealPickupIndex.pickupName))
        //    {
        //        PickupDropletController.CreatePickupDroplet(context.controller.pickupIndex, context.controller.transform.position, new Vector3(UnityEngine.Random.Range(-4f, 4f), 20f, UnityEngine.Random.Range(-4f, 4f)));
        //        context.shouldDestroy = true;
        //    }
        //}
        private void Inventory_SetEquipmentIndex(On.RoR2.Inventory.orig_SetEquipmentIndex orig, Inventory self, EquipmentIndex newEquipmentIndex)
        {
            if (ModConfig.EnableMod.Value)
            {
                if (NetworkServer.active && self)
                {
                    self.gameObject.TryGetComponent(out NetworkIdentity client);
                    CharacterMaster master = null;
                    CharacterBody body = null;
                    foreach (PlayerCharacterMasterController player in PlayerCharacterMasterController.instances)
                    {
                        if (client.netId == player.netId)
                        {
                            master = player.master;
                            body = player.master.GetBody();
                            break;
                        }
                    }
                    if (master && master.teamIndex == TeamIndex.Player)
                    {
                        //Send("Into Inventory_SetEquipmentIndex");
                        foreach (EquipmentStruct @struct in EquipmentStruct.Instance.Values)
                        {
                            if (@struct.EquipmentIndex == newEquipmentIndex && !@struct.Enable)
                            {
                                string equipName = Language.GetString(EquipmentCatalog.GetEquipmentDef(newEquipmentIndex).nameToken);
                                if (client)
                                {
                                    self.SetEquipmentIndex(EquipmentIndex.None);
                                    //PickupDropletController.CreatePickupDroplet(PickupCatalog.FindPickupIndex(newEquipmentIndex),
                                    //                                                    body.corePosition + Vector3.up * 2f, Vector3.up * 20f);
                                    Send($"<style=cIsHealth>{equipName}</style>{Language.GetString(LIMIT_ITEM_PROHIBITED)}", client.clientAuthorityOwner);
                                }
                                //UnityEngine.Object.Destroy(body.equipmentSlot.equipmentIndexgameObject);
                                return;
                            }
                        }
                    }
                }
            }
            orig(self, newEquipmentIndex);
        }

        

        public static void Send(string message)
        {
            Chat.SendBroadcastChat(new Chat.SimpleChatMessage
            {
                baseToken = message
            });
        }
        public static void Send(string message, NetworkConnection networkConnection, short msgType = 59)
        {
            Chat.SimpleChatMessage simpleChat = new Chat.SimpleChatMessage
            {
                baseToken = message,
            };
            NetworkWriter writer = new NetworkWriter();
            writer.StartMessage(msgType);
            writer.Write(simpleChat.GetTypeIndex());
            writer.Write(simpleChat);
            writer.FinishMessage();
            networkConnection?.SendWriter(writer, QosChannelIndex.chat.intVal);
        }
        public static string ItemTierColor(ItemTier tier)
        {
            switch (tier)
            {
                case ItemTier.Tier1:
                    return "cSub";
                case ItemTier.Tier2:
                    return "cIsHealing";
                case ItemTier.Tier3:
                    return "cDeath";
                case ItemTier.Lunar:
                    return "cIsUtility";
                case ItemTier.Boss:
                    return "cShrine";
                case ItemTier.VoidTier1:
                case ItemTier.VoidTier2:
                case ItemTier.VoidTier3:
                case ItemTier.VoidBoss:
                    return "cWorldEvent";
                default:
                    return "cKeywordName";
            }
        }
        internal class ItemStruct
        {
            public string Name { get; set; } = "";
            public ItemIndex ItemIndex { get; set; } = ItemIndex.None;
            public ItemTier ItemTier;
            public int Order;
            public int PickupMax { get; set; } = 0; // 0为标记
            public static Dictionary<string, ItemStruct> Instance { get; set; }

            public ItemStruct(string name, ItemIndex itemIndex, ItemTier itemTier, int order, int pickupMax)
            {
                Name = name;
                ItemIndex = itemIndex;
                ItemTier = itemTier;
                Order = order;
                PickupMax = pickupMax;
            }
        }
        IEnumerator InitItem()
        {
            yield return new WaitForSeconds(2);
            //Send("初始化 InitItem");
            ItemStruct.Instance = new Dictionary<string, ItemStruct>();
            ItemStruct.Instance.Clear();
            //if (ModConfig.EnableTier1Limit.Value) InitItem_Handler(Run.instance.availableTier1DropList);
            //if (ModConfig.EnableTier2Limit.Value) InitItem_Handler(Run.instance.availableTier2DropList);
            //if (ModConfig.EnableTier3Limit.Value) InitItem_Handler(Run.instance.availableTier3DropList);
            //if (ModConfig.EnableBossLimit.Value) InitItem_Handler(Run.instance.availableBossDropList);
            //if (ModConfig.EnableLunarLimit.Value) InitItem_Handler(Run.instance.availableLunarItemDropList);
            //if (ModConfig.EnableVoidLimit.Value)
            //{
            //    InitItem_Handler(Run.instance.availableVoidTier1DropList);
            //    InitItem_Handler(Run.instance.availableVoidTier2DropList);
            //    InitItem_Handler(Run.instance.availableVoidTier3DropList);
            //    InitItem_Handler(Run.instance.availableVoidBossDropList);
            //}
            InitItem_Handler();
            InitItem_Update();
        }
        private void InitItem_Handler()
        {
            ItemIndex itemIndex = (ItemIndex)0;
            ItemIndex itemCount = (ItemIndex)ItemCatalog.itemCount;
            while (itemIndex < itemCount)
            {
                ItemDef itemDef = ItemCatalog.GetItemDef(itemIndex);
                string name = itemDef.name.ToLower(); // 物品代码都变成小写
                ItemStruct.Instance.Add(name, new ItemStruct(name, itemIndex, itemDef.tier, Item_Order(itemDef.tier), pickupMax: -1)); // 固定死 -1 用来判断
                itemIndex++;
            }
        }
        private void InitItem_Handler(List<PickupIndex> pickupIndices)
        {
            foreach (PickupIndex pickupIndex in pickupIndices)
            {
                ItemIndex itemIndex = PickupCatalog.GetPickupDef(pickupIndex).itemIndex;
                ItemDef itemDef = ItemCatalog.GetItemDef(itemIndex);
                string name = itemDef.name.ToLower();
                ItemStruct.Instance.Add(name, new ItemStruct(name, itemIndex, itemDef.tier, Item_Order(itemDef.tier), pickupMax: -1)); // 固定死 -1 用来判断
                //Send($"name = {name}, itemIndex = {itemIndex}");
            }
        }
        private static void InitItem_Update()
        {
            string[] codes = ModConfig.ItemCode.Value.Split(',');
            for (int i = 0; i < codes.Length; i++)
            {
                try
                {
                    string key = codes[i].Split('-')[0].Trim().ToLower();
                    int max = int.Parse(codes[i].Split('-')[1].Trim());
                    if (ItemStruct.Instance.TryGetValue(key, out ItemStruct item))
                    {
                        if (max < 0)
                        {
                            max = 0;
                        }
                        item.PickupMax = max;
                    }
                    else
                    {
                        Debug.LogError($"{codes[i]} is invalid, please check the config file {GUID}.cfg => ItemCode");
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"{codes[i]} is invalid, please check the config file {GUID}.cfg => ItemCode");
                }
                finally
                {

                }
            }
        }
        private int Item_Order(ItemTier tier)
        {
            int result = 0;
            switch (tier)
            {
                case ItemTier.Tier1:
                    result = 1;
                    break;
                case ItemTier.Tier2:
                    result = 2;
                    break;
                case ItemTier.Tier3:
                    result = 3;
                    break;
                case ItemTier.Boss:
                    result = 4;
                    break;
                case ItemTier.Lunar:
                    result = 5;
                    break;
                case ItemTier.VoidTier1:
                case ItemTier.VoidTier2:
                case ItemTier.VoidTier3:
                case ItemTier.VoidBoss:
                    result = 6;
                    break;
                case ItemTier.NoTier:
                    result = 9;
                    break;
            }
            return result;
        }
        internal class EquipmentStruct
        {
            public string Name { get; set; } = "";
            public EquipmentIndex EquipmentIndex { get; set; } = EquipmentIndex.None;
            public bool Enable;
            public static Dictionary<string, EquipmentStruct> Instance { get; set; } = new Dictionary<string, EquipmentStruct>();
            public EquipmentStruct(string name, EquipmentIndex equipmentIndex, bool enable)
            {
                Name = name;
                EquipmentIndex = equipmentIndex;
                Enable = enable;
            }
        }
        IEnumerator InitEquipment()
        {
            yield return new WaitForSeconds(2);
            EquipmentStruct.Instance = new Dictionary<string, EquipmentStruct>();
            EquipmentStruct.Instance.Clear();
            //InitEquipment_Handler(Run.instance.availableEquipmentDropList);
            //InitEquipment_Handler(Run.instance.availableLunarEquipmentDropList);
            InitEquipment_Handler();
            InitEquipment_Update();
        }
        private void InitEquipment_Handler()
        {
            EquipmentIndex equipmentIndex = (EquipmentIndex)0;
            EquipmentIndex equipmentCount = (EquipmentIndex)EquipmentCatalog.equipmentCount;
            while (equipmentIndex < equipmentCount)
            {
                string name = EquipmentCatalog.GetEquipmentDef(equipmentIndex).name.ToLower();
                EquipmentStruct.Instance.Add(name, new EquipmentStruct(name, equipmentIndex, true)); // 默认能拿主动装备
                equipmentIndex++;
            }
        }
        private void InitEquipment_Handler(List<PickupIndex> pickupIndices)
        {
            foreach (PickupIndex pickupIndex in pickupIndices)
            {
                EquipmentIndex equipmentIndex = PickupCatalog.GetPickupDef(pickupIndex).equipmentIndex;
                string name = EquipmentCatalog.GetEquipmentDef(equipmentIndex).name.ToLower();
                EquipmentStruct.Instance.Add(name, new EquipmentStruct(name, equipmentIndex, true)); // 默认能拿主动装备
                //Send($"name = {name}, equipmentIndex = {equipmentIndex}");
            }
        }
        private static void InitEquipment_Update()
        {
            EquipmentStruct.Instance.Values.ForEachTry(x => x.Enable = true);
            string[] codes = ModConfig.EquipmentCode.Value.Split(',');
            for (int i = 0; i < codes.Length; i++)
            {
                string key = codes[i].Trim().ToLower();
                if(EquipmentStruct.Instance.TryGetValue(key, out EquipmentStruct @struct))
                {
                    @struct.Enable = false;
                }
                else
                {
                    Debug.LogError($"{codes[i]} is invalid, please check the config file {GUID}.cfg => EquipmentCode");
                }
            }
        }
        
        [ConCommand(commandName = "show_limit_list", flags = ConVarFlags.ExecuteOnServer, helpText = "View current item restrictions. 查看物品限制情况")]
        public static void Command_ShowItemLimit(ConCommandArgs args)
        {
            int? arg = args.TryGetArgInt(0);
            if (args.Count == 0)
            {
                string info = "===== Limit Items List =====\n";
                List<ItemStruct> listItems = ItemStruct.Instance.Values.ToList().OrderBy(x => x.Order).ThenBy(x => x.Name).ToList();
                info += $"[ Tier 1 ]\n";
                info += ShowItemLimit(listItems, ItemTier.Tier1);
                info += $"[ Tier 2 ]\n";
                info += ShowItemLimit(listItems, ItemTier.Tier2);
                info += $"[ Tier 3 ]\n";
                info += ShowItemLimit(listItems, ItemTier.Tier3);
                info += $"[ Boss ]\n";
                info += ShowItemLimit(listItems, ItemTier.Boss);
                info += $"[ Lunar ]\n";
                info += ShowItemLimit(listItems, ItemTier.Lunar);
                info += $"[ Void ]\n";
                info += ShowItemLimit(listItems, ItemTier.VoidTier1);
                info += ShowItemLimit(listItems, ItemTier.VoidTier2);
                info += ShowItemLimit(listItems, ItemTier.VoidTier3);
                info += ShowItemLimit(listItems, ItemTier.VoidBoss);
                info += "===== Limit Equipments List =====\n";
                List<EquipmentStruct> listEquips = EquipmentStruct.Instance.Values.ToList().OrderBy(x => x.Name).ThenBy(x => x.EquipmentIndex).ToList();
                info += ShowEquipLimit(listEquips);
                Send(info);
            }
        }
        private static string ShowItemLimit(List<ItemStruct> list, ItemTier tier)
        {
            string result = "";
            foreach (ItemStruct item in list)
            {
                if (item.ItemTier == tier)
                {
                    ItemDef itemDef = ItemCatalog.GetItemDef(item.ItemIndex);
                    string itemName = Language.GetString(itemDef.nameToken);
                    result += $"{itemDef.name}({itemName}) = {item.PickupMax}\n";
                }
            }
            return result;
        }
        private static string ShowEquipLimit(List<EquipmentStruct> list)
        {
            string result = "";
            foreach (EquipmentStruct equip in list)
            {
                EquipmentDef itemDef = EquipmentCatalog.GetEquipmentDef(equip.EquipmentIndex);
                string itemName = Language.GetString(itemDef.nameToken);
                result += $"{itemDef.name}({itemName}) = {equip.Enable}\n";
            }
            return result;
        }
        //[ConCommand(commandName = "show_equip_limit_list", flags = ConVarFlags.ExecuteOnServer, helpText = "查看主动装备限制情况")]
        //public static void Command_ShowEquipLimit(ConCommandArgs args)
        //{
        //    int? arg = args.TryGetArgInt(0);
        //    if (args.Count == 0)
        //    {
        //        foreach (KeyValuePair<string, EquipmentStruct> equip in EquipmentStruct.Instance)
        //        {
        //            EquipmentDef itemDef = EquipmentCatalog.GetEquipmentDef(equip.Value.EquipmentIndex);
        //            string itemName = Language.GetString(itemDef.nameToken);
        //            Send($"{itemName} = {equip.Value.Enable}, {equip.Value.EquipmentIndex}");
        //        }
        //    }
        //}
        [ConCommand(commandName = "reload_all_limit", flags = ConVarFlags.ExecuteOnServer, helpText = "重新加载配置文件")]
        public static void Command_ReloadAllLimit(ConCommandArgs args)
        {
            InitItem_Update();
            InitEquipment_Update();
            Debug.Log("Data has been reloaded");
        }
    }

    class ModConfig
    {
        public static ConfigEntry<bool> EnableMod;
        public static ConfigEntry<string> ItemCode;
        public static ConfigEntry<string> EquipmentCode;
        public static ConfigEntry<bool> EnableAutoScrap;
        //public static ConfigEntry<bool> EnableTier1Limit;
        //public static ConfigEntry<bool> EnableTier2Limit;
        //public static ConfigEntry<bool> EnableTier3Limit;
        //public static ConfigEntry<bool> EnableBossLimit;
        //public static ConfigEntry<bool> EnableLunarLimit;
        //public static ConfigEntry<bool> EnableVoidLimit;


        public static void InitConfig(ConfigFile config)
        {
            EnableMod = config.Bind("Setting设置", "EnableMod", true, "Enable Mod. Type the command \"show_limit_list\" on the console to view all current lists.\n启用模组，在控制台输入指令\"show_limit_list\"查看当前所有禁用列表");
            ItemCode = config.Bind("Setting设置", "ItemCode", "LunarDagger-3,ExplodeOnDeath-3,ExecuteLowHealthElite-2,StrengthenBurn-3,Firework-0,Dagger-0,MoreMissile-0",
                "Limit item quantity, Usage: [ItemCode-Count], separated by comma.\n限制物品数量，用法：物品代码-数量，用逗号隔开，注意逗号是英文小写\nhttps://gist.github.com/Lunzir-0325/8f375c6504a64f6c88f35259470659ee");
            EquipmentCode = config.Bind("Setting设置", "EquipmentCode", "CrippleWard,Blackhole,DeathProjectile",
                "Limit equip, Usage: [EquipCode], separated by comma.\n限制物品数量，用法：写上主动装备代码即可，用逗号隔开，注意逗号是英文小写");
            EnableAutoScrap = config.Bind("Setting设置", "EnableAutoScrap", true, "If enabled, items will automatically become scraps, except lunar and void items, will be off the ground.\n启用若超过限制自动成物品碎片，月球装备和虚空装备除外会掉地上。");
            //EnableTier1Limit = config.Bind("Setting设置", "EnableTier1Limit", true, "Enable tier1(white) item limit.\n启用白色物品数量限制");
            //EnableTier2Limit = config.Bind("Setting设置", "EnableTier2Limit", true, "Enable tier2(green) item limit.\n启用绿色物品数量限制");
            //EnableTier3Limit = config.Bind("Setting设置", "EnableTier3Limit", true, "Enable tier3(red) item limit.\n启用红色物品数量限制");
            //EnableBossLimit = config.Bind("Setting设置", "EnableBossLimit", true, "Enable boss(yellow) item limit.\n启用黄色物品数量限制");
            //EnableLunarLimit = config.Bind("Setting设置", "EnableLunarLimit", true, "Enable lunar(blue) item limit.\n启用蓝色物品数量限制");
            //EnableVoidLimit = config.Bind("Setting设置", "EnableVoidLimit", true, "Enable void(purple) item limit.\n启用紫色物品数量限制");
            //EnableTier1Limit.Value = true;
            //EnableTier2Limit.Value = true;
            //EnableTier3Limit.Value = true;
            //EnableBossLimit.Value = true;
            //EnableLunarLimit.Value = true;
            //EnableVoidLimit.Value = true;
        }
    }

    public static class Tokens
    {
        internal static string LanguageRoot
        {
            get
            {
                return System.IO.Path.Combine(AssemblyDir, "Language");
            }
        }

        internal static string AssemblyDir
        {
            get
            {
                return System.IO.Path.GetDirectoryName(ItemLimited.PluginInfo.Location);
            }
        }
        public static void RegisterLanguageTokens()
        {
            On.RoR2.Language.SetFolders += Language_SetFolders;
        }

        private static void Language_SetFolders(On.RoR2.Language.orig_SetFolders orig, Language self, IEnumerable<string> newFolders)
        {
            if (Directory.Exists(LanguageRoot))
            {
                IEnumerable<string> second = Directory.EnumerateDirectories(System.IO.Path.Combine(new string[]
                {
                    LanguageRoot
                }), self.name);
                orig.Invoke(self, newFolders.Union(second));
            }
            else
            {
                orig.Invoke(self, newFolders);
            }
        }
    }
}
