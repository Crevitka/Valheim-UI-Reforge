using BepInEx;
using UnityEngine;

public class HideUIElementsMod : BaseUnityPlugin
{
    public static void HideMerchAndModMessage()
    {
        // Ищем объект, который отображает сообщение о модах
        GameObject modMessageObject = GameObject.Find("modded_text"); // Подберите правильное имя объекта
        if (modMessageObject != null)
        {
            modMessageObject.SetActive(false); // Отключаем объект с сообщением
            GameObject.Destroy(modMessageObject);
            Debug.Log("Mod message hidden.");
        }
        else
        {
            Debug.LogWarning("Mod message object not found.");
        }

        // Ищем объект, который отображает Merch Store
        GameObject BexInExObject = GameObject.Find("BexInEx Version"); // Подберите правильное имя объекта
        if (BexInExObject != null)
        {
            BexInExObject.SetActive(false); // Отключаем Merch Store
            Debug.Log("Merch Store hidden.");
        }
        else
        {
            Debug.LogWarning("Merch Store object not found.");
        }

        GameObject showlogObject = GameObject.Find("showlog"); // Подберите правильное имя объекта
        if (showlogObject != null)
        {
            showlogObject.SetActive(false); // Отключаем Merch Store
            Debug.Log("Merch Store hidden.");
        }
        else
        {
            Debug.LogWarning("Merch Store object not found.");
        }

        // Ищем объект, который отображает Merch Store
        GameObject merchStoreObject = GameObject.Find("TopRight"); // Подберите правильное имя объекта
        if (merchStoreObject != null)
        {
            merchStoreObject.SetActive(false); // Отключаем Merch Store
            Debug.Log("Merch Store hidden.");
        }
        else
        {
            Debug.LogWarning("Merch Store object not found.");
        }
    }
}
