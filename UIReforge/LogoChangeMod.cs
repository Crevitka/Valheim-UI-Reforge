using BepInEx;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;  // Добавляем для работы с SceneManager
using System.IO;

[BepInDependency(Jotunn.Main.ModGuid)]
public class LogoChangeMod : BaseUnityPlugin
{
    // Метод, который вызывается при полной загрузке сцены
    public static void ChangeLogo()
    {
        ChangeLogo("LOGO");
        ChangeLogo("AshlandsLogo");
        ChangeLogo("AshlandsLogo_Glow");
    }

    private static void ChangeLogo(string name)
    {
        GameObject logoObject = GameObject.Find(name);

        if (logoObject != null)
        {
            Image logoImage = logoObject.GetComponent<Image>();

            if (logoImage != null)
            {
                Sprite newLogo = LoadNewLogo(name == "LOGO" ? "Logo.png" : "UnderLogo.png");
                if (newLogo != null)
                {
                    logoImage.sprite = newLogo;
                    Debug.Log("Main Logo successfully changed.");
                }
                else
                {
                    Debug.LogWarning("New logo sprite not found.");
                }
            }
            else
            {
                Debug.LogWarning("Main Logo Image component not found.");
            }
        }
        else
        {
            Debug.LogWarning("Main Logo object not found.");
        }
    }


    // Метод для загрузки нового логотипа из файла
    private static Sprite LoadNewLogo(string name)
    {
        // Укажите путь к вашему новому логотипу (например, рядом с вашим плагином)
        string filePath = Path.Combine(Paths.PluginPath, name);

        if (File.Exists(filePath))
        {
            // Загружаем изображение в текстуру
            byte[] fileData = File.ReadAllBytes(filePath);
            Texture2D texture = new Texture2D(2, 2);
            texture.LoadImage(fileData);  // Метод LoadImage из UnityEngine.ImageConversion

            // Создаем спрайт из текстуры
            return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
        }
        else
        {
            Debug.LogError($"Logo file not found: {filePath}");
            return null;
        }
    }
}
