using BepInEx;
using HarmonyLib;
using UnityEngine.SceneManagement;  // Добавляем для работы с SceneManager

[BepInPlugin("com.uiReforge.valheimmod", "Valheim UI Mod", "1.0.0")]
[BepInDependency(Jotunn.Main.ModGuid)]
public class ValheimMod : BaseUnityPlugin
{
    private Harmony harmony;

    private void Awake()
    {
        harmony = new Harmony("com.uiReforge.valheimmod");
        harmony.PatchAll();

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "start")
        {
            HideUIElementsMod.HideMerchAndModMessage();
            LogoChangeMod.ChangeLogo();
        }
    }
}
