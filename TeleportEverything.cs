using System.Collections.Generic;
using System.IO;
using BepInEx;
using HarmonyLib;
using System;
using BepInEx.Configuration;
using UnityEngine;
using Random = UnityEngine.Random;

namespace TeleportEverything
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    
    internal class SmartSave : BaseUnityPlugin
    {
        public const string PluginGUID = "com.kpro.TeleportEverything";
        public const string PluginName = "TeleportEverything";
        public const string PluginVersion = "1.1";

       private readonly Harmony harmony = new Harmony("com.kpro.TeleportEverything");

        // General
        public static ConfigEntry<bool> EnableMod;
        public static ConfigEntry<string> TeleportMode;

        // Transport Allies
        public static bool TransportAllies;
        public static ConfigEntry<bool> TransportBoar;
        public static ConfigEntry<bool> TransportWolves;
        public static ConfigEntry<bool> TransportLox;
        public static ConfigEntry<string> TransportMask;
        public static ConfigEntry<float> TransportRadius;
        public static ConfigEntry<float> TransportVerticalTolerance;
        public static bool IncludeTamed;
        public static bool IncludeNamed;
        public static bool  IncludeWild;
        public static bool IncludeFollow;
        public static ConfigEntry<string> IncludeMode;

        //Teleport Self
        public static ConfigEntry<float> SearchRadius;
        public static ConfigEntry<float> MaximumDisplacement;
        public static List<Character> enemies;
        public static List<Character> transportTargets;

        private void Awake()
        {
            harmony.PatchAll();
            CreateConfigValues();
            enemies = new List<Character>();
            transportTargets = new List<Character>();

            TransportAllies = false;
            IncludeTamed = false;
            IncludeNamed = false;
            IncludeWild = false;
            IncludeFollow = false;

        }

        private void OnDestroy()
        {
            harmony.UnpatchSelf();
        }


        private void CreateConfigValues()
        {
            Config.SaveOnConfigSet = true;

            // General
            EnableMod = Config.Bind("Mod", "Enable Mod", true);

            // Transport
            
            TransportBoar = Config.Bind("Transport", "Transport Boar", false);
            TransportWolves = Config.Bind("Transport", "Transport Wolves", false);
            TransportLox = Config.Bind("Transport", "Transport Lox", false);
            TransportMask = Config.Bind("Transport", "Transport Mask", "");

            IncludeMode = Config.Bind("Transport", "Ally Mode", "No Allies",
                new ConfigDescription("Ally Mode",
                    new AcceptableValueList<string>("No Allies", "All tamed", "Only Follow",
                        "All tamed except Named", "Only Named")));
            
            TransportRadius = Config.Bind("Transport", "Transport Radius", 10f);
            TransportVerticalTolerance =
                Config.Bind("Transport", "Transport Vertical Tolerance", 2f);

            // Teleport Self
            SearchRadius = Config.Bind("Teleport Self", "Search Radius", 10f);
            MaximumDisplacement = Config.Bind("Teleport Self", "Max Enemy Displacement", 5f);
            TeleportMode = Config.Bind("Teleport Self", "Teleport Mode", "Standard",
                new ConfigDescription("Teleport Mode",
                    new AcceptableValueList<string>("Standard", "Vikings Don't Run",
                        "Take Them With You")));


            
            
        }

        public static void SetIncludeMode()
        {
            if (IncludeMode.Value.Contains("No Allies"))
            {
                TransportAllies = false;
                IncludeTamed = false;
                IncludeNamed = false;
                IncludeFollow = false;
                IncludeWild = false;
            }
            else
            {
                TransportAllies = true;
            }

            if (IncludeMode.Value.Contains("All tamed"))
            {
                IncludeTamed = true;
                IncludeNamed = true;
                IncludeFollow = true;
                IncludeWild = false;
            }
            
            if(IncludeMode.Value.Contains("Only Follow"))
            {
                IncludeTamed = false;
                IncludeNamed = false;
                IncludeFollow = true;
                IncludeWild = false;
            }
            if(IncludeMode.Value.Contains("All tamed except Named"))
            {
                IncludeTamed = true;
                IncludeNamed = false;
                IncludeFollow = true;
                IncludeWild = false;
            }
            if(IncludeMode.Value.Contains("Only Named"))
            {
                IncludeTamed = false;
                IncludeNamed = true;
                IncludeFollow = false;
                IncludeWild = false;
            }
            
            
            
        }
        public static List<Character> GetEnemies()
        {
            PopulateEntityLists();

            return enemies;
        }

        public static List<Character> GetTransportTargets()
        {
            PopulateEntityLists();

            return transportTargets;
        }

        public static float CalcDistToEntity(Character e)
        {
            return VectorToEntity(e).magnitude;
        }

        public static Vector3 VectorToEntity(Character e)
        {
            return e.transform.position - Player.m_localPlayer.transform.position;
        }

        public static float HorizontalDistance(Character e)
        {
            Vector3 v3 = VectorToEntity(e);
            Vector2 v2 = new Vector2(v3.x, v3.z);
            return v2.magnitude;
        }

        public static float VerticalDistance(Character e)
        {
            return VectorToEntity(e).y;
        }

        public static bool IsAlly(Character c)
        {
            if (c.m_name.ToLower().Contains("wolf") && TransportWolves.Value)
                return true;
            else if (c.name.ToLower().Contains("boar") && TransportBoar.Value)
                return true;
            else if (c.name.ToLower().Contains("lox") && TransportLox.Value)
                return true;
            else if (c.name.ToLower().Contains(TransportMask.Value.ToLower()) &&
                     TransportMask.Value != "")
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public static bool IsAllyTransportable(Character ally)
        {
            if (IsFollow(ally) && IncludeFollow)
            {
                return true;
            }
            if (IsNamed(ally) && IncludeNamed)
            {
                return true;
            }
            else if (IsNamed(ally) && !IncludeNamed)
            {
                return false;
            }

            if (ally.IsTamed() && IncludeTamed)
            {
                return true;
            }


   

            return false;
        }


        public static bool IsNamed(Character t)
        {
            
            return !t.GetHoverName().Equals("Wolf");
        }

        public static bool IsFollow(Character f)
        {
            MonsterAI mAi = f.GetComponent<MonsterAI>();
            if (mAi.GetFollowTarget() != null && mAi.GetFollowTarget().Equals(Player.m_localPlayer.gameObject))
            {
                return true;
            }

            return false;
        }

        private static void PopulateEntityLists()
        {
            transportTargets.Clear();
            enemies.Clear();

            Character[] entityArray = FindObjectsOfType<Character>();


            foreach (Character c in entityArray)
            {
                if (IsAlly(c) && IsAllyTransportable(c) && TransportAllies)
                {
                    if (HorizontalDistance(c) <= TransportRadius.Value &&
                        VerticalDistance(c) <= TransportVerticalTolerance.Value)
                    {
                        transportTargets.Add(c);
                    }
                }

                if (c.GetComponent<BaseAI>() != null &&
                    c.GetComponent<BaseAI>().IsEnemey(Player.m_localPlayer) && !c.IsTamed())
                {
                    if (CalcDistToEntity(c) <= SearchRadius.Value)
                    {
                        enemies.Add(c);
                    }
                }
            }
        }

        [HarmonyPatch(typeof(Player))]
        [HarmonyPatch("TeleportTo")]
        public class TeleportTo_Patch
        {
            static bool Postfix(bool __result, Player __instance, Vector3 pos, Quaternion rot,
                bool distantTeleport)
            {
                if (!EnableMod.Value)
                {
                    return __result;
                }

                if (GetEnemies().Count > 0 && TeleportMode.Value.Contains("Take"))
                {
                    MessageHud.instance.ShowMessage(MessageHud.MessageType.Center,
                        $"Taking Enemies With You! {GetEnemies().Count} enemies charge the portal!!!");
                    foreach (Character e in enemies)
                    {
                        if (UnityEngine.Random.Range(0, 100) <= 25)
                        {
                            Vector3 offset = Random.insideUnitSphere * MaximumDisplacement.Value;
                            offset.y = 0;
                            e.transform.position = __instance.m_teleportTargetPos + offset;
                        }
                    }

                    return __result;
                }

                if (GetTransportTargets().Count > 0 && TransportAllies)
                {
                    foreach (Character ally in transportTargets)
                    {
                        Vector3 offset = __instance.m_lookDir * 1;
                        offset.y = 0;
                        ally.transform.position = __instance.m_teleportTargetPos + offset;
                    }

                    return __result;
                }


                return __result;
            }
        }


        [HarmonyPatch(typeof(Humanoid))]
        [HarmonyPatch("IsTeleportable")]
        public class IsTeleportable_Patch
        {
            static bool Postfix(bool __result, Humanoid __instance)
            {

                SetIncludeMode();
                
                if (!EnableMod.Value)
                {
                    return __result;
                }


                if (TransportAllies)
                {
                    MessageHud.instance.ShowMessage(MessageHud.MessageType.Center,
                        $"{GetTransportTargets().Count} allies will teleport with you!");
                }

                if (GetEnemies().Count > 0)
                {
                    if (TeleportMode.Value.Contains("Run"))
                    {
                        MessageHud.instance.ShowMessage(MessageHud.MessageType.Center,
                            $"Vikings Don't run from a fight: {GetEnemies().Count} enemies with in {SearchRadius.Value} meters.");
                        return false;
                    }
                    else if (TeleportMode.Value.Contains("Take"))

                    {
                        MessageHud.instance.ShowMessage(MessageHud.MessageType.Center,
                            $"Beware: {GetEnemies().Count} enemies may charge the portal!");
                    }
                }


                return __result;
            }
        }
    }
}