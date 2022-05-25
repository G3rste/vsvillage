using System;
using System.Reflection;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

namespace VsVillage
{
    public class BedPatch
    {
        public static void Patch(Harmony harmony)
        {
            harmony.Patch(methodInfo(),
                prefix: new HarmonyMethod(typeof (BedPatch)
                        .GetMethod("Prefix",
                        BindingFlags.Static | BindingFlags.Public)));
        }

        public static void Unpatch(Harmony harmony)
        {
            harmony.Unpatch(methodInfo(),
                HarmonyPatchType.Prefix,
                "gerste.vsvillage");
        }

        public static MethodInfo methodInfo()
        {
            return typeof (BlockEntityBed).GetMethod("DidMount",
                BindingFlags.Instance | BindingFlags.Public);
        }

        public static bool
        Prefix(BlockEntityBed __instance, EntityAgent entityAgent)
        {
            if (__instance.Api == null)
            {
                __instance.Api = entityAgent.Api;
            }
            return true;
        }
    }
}
