using BepInEx;
using UnityEngine;
using UnityEngine.UI;

[BepInPlugin("com.yourname.replaceallbuttonsmod", "Replace All Buttons Mod", "1.0.0")]
public class ReplaceAllButtonsMod : BaseUnityPlugin
{
    private AssetBundle customButtonBundle;
    private GameObject customButtonPrefab;
    private void Start()
    {
        // Запускаем функцию замены кнопок каждые 2 секунды
        InvokeRepeating("ReplaceAllButtons", 2.0f, 2.0f);
    }
    private void Awake()
    {
        // Загружаем AssetBundle с кнопками
        customButtonBundle = AssetBundle.LoadFromFile(Paths.PluginPath + "/custom_buttons");
        Jotunn.Logger.LogInfo($"prefab btn {customButtonPrefab}");
        if (customButtonBundle == null)
        {
            Debug.LogError("Не удалось загрузить btn Bundle!");
        }
        customButtonPrefab = customButtonBundle.LoadAsset<GameObject>("CustomButton");
        if (customButtonPrefab == null)
        {
            Logger.LogError("Failed to load custom button prefab from AssetBundle!");
        }
        else
        {
            Logger.LogInfo("Custom button prefab loaded successfully.");
        }


        // Запускаем замену кнопок
        ReplaceAllButtons();
    }

    private void ReplaceAllButtons()
    {
        // Найти все кнопки в интерфейсе игры
        Button[] allButtons = GameObject.FindObjectsOfType<Button>();
        // Logger.LogInfo($"Found {allButtons.Length} buttons to replace.");

        foreach (Button btn in allButtons)
        {
            // Logger.LogInfo($"Replacing button: {btn.name}");
            ReplaceButton(btn);
        }
    }

    private void ReplaceButton(Button button)
    {
        // Применить кастомный внешний вид кнопки
        if (customButtonPrefab != null)
        {
            Image originalImage = button.GetComponent<Image>();
            if (originalImage == null)
            {
                // Logger.LogWarning($"Button {button.name} has no Image component.");
                return;
            }
            Image customImage = customButtonPrefab.GetComponent<Image>();
            if (customImage == null)
            {
                // Logger.LogError("Custom button prefab has no Image component.");
                return;
            }

            // Заменить текстуру кнопки
            originalImage.sprite = customImage.sprite;
            originalImage.color = customImage.color;
            // Logger.LogInfo($"Replaced button: {button.name}");
            // Добавить вашу логику при нажатии на кнопку
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() =>
            {
                Logger.LogInfo("Custom Button Clicked!");
            });
        }
    }
}
