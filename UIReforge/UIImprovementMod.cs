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
        private static GameObject modEditPanel;

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
                CreateModEditButton(__instance);
                CreateToggleChangeLogButton(__instance);
            }

            private static void CreateModEditButton(FejdStartup fejdStartup)
            {
                GameObject menuList = fejdStartup.m_menuList;
                Transform menuEntries = menuList.transform.Find("MenuEntries");
                Transform settingsButtonTransform = menuEntries.transform.Find("Settings");

                GameObject modEditButton = new GameObject(
                    "ModEditButton",
                    typeof(RectTransform),
                    typeof(Button),
                    typeof(CanvasRenderer)
                );

                modEditButton.transform.SetParent(menuEntries, false);

                Button buttonComponent = modEditButton.GetComponent<Button>();
                Button settingsButtonComponent = settingsButtonTransform.GetComponent<Button>();

                buttonComponent.transition = settingsButtonComponent.transition;
                buttonComponent.colors = settingsButtonComponent.colors;
                buttonComponent.navigation = settingsButtonComponent.navigation;
                buttonComponent.onClick.AddListener(ToggleModEditPanel);

                GameObject textObject = new GameObject(
                    "Text",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(TextMeshProUGUI)
                );
                textObject.transform.SetParent(modEditButton.transform, false);

                RectTransform textRectTransform = textObject.GetComponent<RectTransform>();
                textRectTransform.anchorMin = new Vector2(0, 0);
                textRectTransform.anchorMax = new Vector2(1, 1);
                textRectTransform.offsetMin = Vector2.zero;
                textRectTransform.offsetMax = Vector2.zero;
                textRectTransform.localScale = Vector3.one;

                TextMeshProUGUI buttonText = textObject.GetComponent<TextMeshProUGUI>();
                TextMeshProUGUI settingsButtonText = settingsButtonTransform.GetComponentInChildren<TextMeshProUGUI>();

                buttonText.text = "Edit Mods";
                buttonText.font = settingsButtonText.font;
                buttonText.fontSize = settingsButtonText.fontSize;
                buttonText.alignment = settingsButtonText.alignment;
                buttonText.color = settingsButtonText.color;
                buttonText.enableAutoSizing = settingsButtonText.enableAutoSizing;
                buttonText.raycastTarget = true;

                buttonComponent.targetGraphic = buttonText;

                modEditButton.AddComponent<HorizontalLayoutGroup>();
                modEditButton.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                Animator settingsAnimator = settingsButtonTransform.GetComponent<Animator>();
                if (settingsAnimator != null)
                {
                    Animator animator = modEditButton.AddComponent<Animator>();
                    animator.runtimeAnimatorController = settingsAnimator.runtimeAnimatorController;
                }

                modEditButton.transform.SetSiblingIndex(settingsButtonTransform.GetSiblingIndex());

                CreateModEditPanel(fejdStartup);
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

            private static void CreateModEditPanel(FejdStartup fejdStartup)
            {
                GameObject mainMenu = fejdStartup.m_mainMenu;

                modEditPanel = new GameObject("ModEditPanel");
                modEditPanel.transform.SetParent(mainMenu.transform, false);

                RectTransform rectTransform = modEditPanel.AddComponent<RectTransform>();
                rectTransform.sizeDelta = new Vector2(400, 600);

                Image panelImage = modEditPanel.AddComponent<Image>();
                panelImage.color = new Color(0, 0, 0, 0.8f);

                modEditPanel.SetActive(false);
            }

            private static void ToggleModEditPanel()
            {
                if (modEditPanel != null)
                {
                    modEditPanel.SetActive(!modEditPanel.activeSelf);
                }
                else
                {
                    Debug.LogWarning("[UIReforge] ModEditPanel object not found!");
                }
            }
        }
    }
}
