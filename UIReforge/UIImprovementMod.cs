using BepInEx;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UIReforge
{
    [BepInPlugin(ModGuid, ModName, Version)]
    [BepInDependency(Jotunn.Main.ModGuid, BepInDependency.DependencyFlags.SoftDependency)]
    public class ValheimUIMod : BaseUnityPlugin
    {
        public const string ModGuid = "viktor.UIReforge";
        public const string ModName = "UI Reforge";
        public const string Version = "1.0.0";

        private Harmony _harmony;

        private void Awake()
        {
            _harmony = new Harmony(ModGuid);

            // Патчи из этого файла (FejdStartup / меню)
            _harmony.PatchAll(typeof(ValheimUIMod));

            // Патчи HUD из отдельного файла HudPatch.cs
            _harmony.PatchAll(typeof(HudPatch));

            Logger.LogInfo("[UIReforge] Awake -> patches applied");
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
        }

        // ========= ПАТЧ МЕНЮ =========
        [HarmonyPatch(typeof(FejdStartup), "SetupGui")]
        public static class Patch_MainMenuSetup
        {
            public static void Postfix(FejdStartup __instance)
            {
                CreateToggleChangeLogButton(__instance);
            }

            private static void CreateToggleChangeLogButton(FejdStartup fejdStartup)
            {
                GameObject changeLogPanel = Object.FindObjectOfType<ChangeLog>()?.gameObject;
                if (changeLogPanel != null)
                {
                    changeLogPanel.SetActive(false);
                }
                else
                {
                    Debug.LogWarning("[UIReforge] ChangeLog object not found when trying to hide it.");
                }
            }
        }
    }
}
