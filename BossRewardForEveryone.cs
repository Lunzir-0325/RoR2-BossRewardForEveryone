using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using R2API.Utils;
using RoR2;
using RoR2.Networking;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

namespace BossRewardForEveryone
{
    [BepInDependency("com.bepis.r2api")]
    [BepInPlugin(GUID, modname, modver)]
    [NetworkCompatibility(CompatibilityLevel.NoNeedForSync, VersionStrictness.DifferentModVersionsAreOk)]
    public class BossRewardForEveryone :BaseUnityPlugin
    {
        public const string GUID = "com.Lunzir.BossRewardForEveryone", modname = "BossRewardForEveryone", modver = "1.0.4";
        //public void Start()
        //{
        //    if (ModConfig.EnableMod.Value)
        //    {
        //        foreach (var plugin in Chainloader.PluginInfos)
        //        {
        //            if (plugin.Key == "com.evaisa.moreshrines")
        //            {
        //                ModConfig.MoreShrines = plugin.Value.Instance;
        //            }
        //        }
        //    }
        //}
        public void Awake()
        {
            ModConfig.InitConfig(Config);
            if (ModConfig.EnableMod.Value)
            {
                On.RoR2.Run.Start += Run_Start;
                On.RoR2.BossGroup.DropRewards += BossGroup_DropRewards;
            }
        }

        private void Run_Start(On.RoR2.Run.orig_Start orig, Run self)
        {
            orig(self);
            ArtifactDef artifactDef = ArtifactCatalog.FindArtifactDef("Command");
            if (RunArtifactManager.instance.IsArtifactEnabled(artifactDef))
            {
                On.RoR2.BossGroup.DropRewards -= BossGroup_DropRewards;
            }
            else
            {
                On.RoR2.BossGroup.DropRewards += BossGroup_DropRewards;
            }
        }

        private void BossGroup_DropRewards(On.RoR2.BossGroup.orig_DropRewards orig, BossGroup self)
        {
            // SuperRoboBallEncounter
            //Send($"name={self.name}");
            Xoroshiro128Plus rng = Reflection.GetFieldValue<Xoroshiro128Plus>(self, "rng");
            List<PickupIndex> bossDrops = Reflection.GetFieldValue<List<PickupIndex>>(self, "bossDrops");
            List<PickupDropTable> bossDropTables = Reflection.GetFieldValue<List<PickupDropTable>>(self, "bossDropTables");

            bool result = false;
            if (!ModConfig.EnableBossSuperRoboBallShare.Value 
                && SceneManager.GetActiveScene().name.ToLower() == "shipgraveyard" 
                && self.name.StartsWith("SuperRoboBallEncounter"))
            {
                result = true;
            }
            int participatingPlayerCount = Run.instance.participatingPlayerCount;
            if (participatingPlayerCount != 0 && self.dropPosition)
            {
                PickupIndex pickupIndex = PickupIndex.none;
                if (self.dropTable)
                {
                    pickupIndex = self.dropTable.GenerateDrop(rng);
                }
                else
                {
                    List<PickupIndex> list = Run.instance.availableTier2DropList;
                    if (self.forceTier3Reward)
                    {
                        list = Run.instance.availableTier3DropList;
                    }
                    pickupIndex = rng.NextElementUniform<PickupIndex>(list);
                }
                int num = 1 + self.bonusRewardCount;
                Vector3 vector = default;
                Quaternion rotation = default;
                if (result)
                {
                    if (self.scaleRewardsByPlayerCount)
                    {
                        num *= participatingPlayerCount;
                    }
                    float angle = 360f / (float)num;
                    vector = Quaternion.AngleAxis((float)UnityEngine.Random.Range(0, 360), Vector3.up) * (Vector3.up * 40f + Vector3.forward * 5f);
                    rotation = Quaternion.AngleAxis(angle, Vector3.up); 
                }
                int i = 0;
                while (i < num)
                {
                    PickupIndex pickupIndex2 = pickupIndex;
                    if ((bossDrops.Count > 0 || bossDropTables.Count > 0) && rng.nextNormalizedFloat <= self.bossDropChance)
                    {
                        if (bossDropTables.Count > 0)
                        {
                            pickupIndex2 = rng.NextElementUniform<PickupDropTable>(bossDropTables).GenerateDrop(rng);
                        }
                        else
                        {
                            pickupIndex2 = rng.NextElementUniform<PickupIndex>(bossDrops);
                        }
                    }
                    if (result)
                    {
                        PickupDropletController.CreatePickupDroplet(pickupIndex2, self.dropPosition.position, vector);
                        vector = rotation * vector;
                    }
                    else
                    {
                        ForEveryone(pickupIndex2);
                    }
                    i++;
                }
            }
        }

        private void ForEveryone(PickupIndex pickupIndex)
        {
            for (int i = 0; i < PlayerCharacterMasterController.instances.Count; i++)
            {
                try
                {
                    NetworkUser networkUser = PlayerCharacterMasterController.instances[i].networkUser;
                    if (networkUser && networkUser.isActiveAndEnabled)
                    {
                        PickupDef pickupDef = PickupCatalog.GetPickupDef(pickupIndex);
                        CharacterBody body = networkUser.master.GetBody();
                        CharacterMaster master = networkUser.master;
                        //ItemDef itemDef = ItemCatalog.GetItemDef(pickupDef.itemIndex);

                        body.inventory.GiveItem(pickupDef.itemIndex);

                        Lunzir.Helper.ChatHelper.Send(master, pickupIndex);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError(ex);
                }
                finally
                {

                }
            }
        }
    }

    class ModConfig
    {
        public static ConfigEntry<bool> EnableMod;
        public static ConfigEntry<bool> EnableBossSuperRoboBallShare;
        //public static BaseUnityPlugin MoreShrines;

        public static void InitConfig(ConfigFile config)
        {
            EnableMod = config.Bind("Setting设置", "EnableMod", true, "If enable the mod, when a boss is defeated, loot is automatically delivered to each bag, " +
                "and automatically disabled when open artifact of Command.\n启用模组，当打败boss，战利品自动送到每位包里，统帅模式下自动取消模组功能，可与共享Mod共用。");
            EnableBossSuperRoboBallShare = config.Bind("Setting设置", "EnableBossSuperRoboBallShare", false, "If disable, red item won't be share in the shipgraveyard map when knock the eggs.\n" +
                "启用地图塞壬召唤蛋蛋boss红装分享，true = 开启， false = 扔地上");
        }
    }
}
