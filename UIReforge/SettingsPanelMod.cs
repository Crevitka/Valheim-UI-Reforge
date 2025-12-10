using BepInEx;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;


[BepInPlugin("com.yourname.settingsui", "Settings UI Mod", "1.0.0")]

public class SettingsUIMod : BaseUnityPlugin
{
    private GameObject settingsUI;
    private AssetBundle assetBundle;

    private void Awake()
    {
        // Загрузка нового интерфейса настроек
        LoadSettingsUI();
    }

    private void LoadSettingsUI()
    {
        // Загрузка вашего AssetBundle
        assetBundle = LoadAssetBundle("settingspanel");
        Jotunn.Logger.LogInfo($"settingpanel AssetBundle {assetBundle}");
        if (assetBundle == null)
        {
            Debug.LogError("Не удалось загрузить settingpanel AssetBundle!");
        }

        settingsUI = assetBundle.LoadAsset<GameObject>("SettingsUI");
    }

    private AssetBundle LoadAssetBundle(string bundleName)
    {
        var assetBundlePath = Path.Combine(BepInEx.Paths.PluginPath, bundleName); // Путь к папке плагинов
        if (File.Exists(assetBundlePath))
        {
            Jotunn.Logger.LogInfo($"Loading AssetBundle from {assetBundlePath}");
            return AssetBundle.LoadFromFile(assetBundlePath);
        }
        else
        {
            Jotunn.Logger.LogError($"AssetBundle {bundleName} not found at path {assetBundlePath}");
            return null;
        }
    }

    void Start()
    {
        var settingsButton = GameObject.Find("SettingsButton");
        if (settingsButton != null)
        {
            settingsButton.GetComponent<Button>().onClick.AddListener(OnSettingsButtonClicked);
        }
    }

    void OnSettingsButtonClicked()
    {
        StartCoroutine(WaitForSettingsPanel());
    }

    IEnumerator WaitForSettingsPanel()
    {
        GameObject settingsPanel = null;

        while (settingsPanel == null)
        {
            settingsPanel = GameObject.Find("Settings(Clone)");
            yield return null; // Ждем следующего кадра
        }

        Logger.LogInfo("Панель настроек найдена!");

        // Добавление новой вкладки в настройки
        AddSettingsTab();
        // Дальнейшие действия с панелью
    }


    private void AddSettingsTab()
    {
        // Получение существующего меню настроек
        var settingsPanel = GameObject.Find("Settings(Clone)");
        if (settingsPanel == null)
        {
            Debug.LogError("Не удалось найти панель настроек!");
            return;
        }

        // Создание кнопки для новой вкладки
        var tabButton = new GameObject("NewTabButton", typeof(Button), typeof(Image));
        tabButton.transform.SetParent(settingsPanel.transform);

        // Настройка кнопки
        var buttonComponent = tabButton.GetComponent<Button>();
        buttonComponent.onClick.AddListener(() =>
        {
            // Логика переключения вкладок
            settingsUI.SetActive(true);
        });

        // Добавление вашего интерфейса как дочернего объекта
        settingsUI.transform.SetParent(settingsPanel.transform);
        settingsUI.SetActive(false); // Скрываем вкладку по умолчанию
    }
}
