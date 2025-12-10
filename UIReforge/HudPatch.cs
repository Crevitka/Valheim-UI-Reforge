using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UIReforge
{
    [HarmonyPatch]
    public static class HudPatch
    {
        private class FoodSlotUI
        {
            public GameObject Root;
            public Image Icon;
            public TextMeshProUGUI Timer;
        }

        private static bool _initialized;
        private static GameObject _panel;

        // HP
        private static Image _hpFastImage;   // красная полоса (moment)
        private static Image _hpSlowImage;   // жёлтая полоса (delay)
        private static TextMeshProUGUI _hpText;

        private static float _fastValue = -1f;
        private static float _slowValue = -1f;

        // еда
        private static readonly FoodSlotUI[] _slots = new FoodSlotUI[3];

        // поле с ItemDrop.ItemData внутри Player.Food
        private static FieldInfo _foodItemField;

        // ---------- ИНИЦИАЛИЗАЦИЯ КАСТОМНОГО HUD ----------

        private static void EnsureInit(Hud hud)
        {
            if (_initialized) return;
            _initialized = true;

            try
            {
                string bundlePath = Path.Combine(Paths.PluginPath, "UIReforge", "myhub");
                var bundle = AssetBundle.LoadFromFile(bundlePath);
                if (bundle == null)
                {
                    Debug.LogError("[UIReforge] Cannot load AssetBundle: " + bundlePath);
                    return;
                }

                GameObject prefab = bundle.LoadAsset<GameObject>("healthpanel");
                if (prefab == null)
                {
                    Debug.LogError("[UIReforge] Prefab 'healthpanel' not found in bundle");
                    return;
                }

                _panel = UnityEngine.Object.Instantiate(prefab, hud.m_rootObject.transform);
                _panel.name = "CustomHealthPanel";

                FindReferences(_panel);

                // спрячем ванильную панель, но не трогаем логику
                if (hud.m_healthPanel != null)
                    hud.m_healthPanel.gameObject.SetActive(false);

                Debug.Log("[UIReforge] Custom HUD initialized");
            }
            catch (Exception ex)
            {
                Debug.LogError("[UIReforge] Init error: " + ex);
            }
        }

        private static void FindReferences(GameObject root)
        {
            // --------- HEALTH ---------
            Transform healthTr = root.transform.Find("Health");
            if (healthTr != null)
            {
                Transform fastTr = healthTr.Find("fast/bar");
                Transform slowTr = healthTr.Find("slow/bar");

                if (fastTr != null)
                {
                    _hpFastImage = fastTr.GetComponent<Image>();
                    if (_hpFastImage != null)
                    {
                        // гарантируем корректный режим fill
                        _hpFastImage.type = Image.Type.Filled;
                        _hpFastImage.fillMethod = Image.FillMethod.Horizontal;
                        _hpFastImage.fillOrigin = 0;

                        // если нет спрайта – создадим из mainTexture
                        if (_hpFastImage.sprite == null && _hpFastImage.mainTexture is Texture2D tex)
                        {
                            _hpFastImage.sprite = Sprite.Create(
                                tex,
                                new Rect(0, 0, tex.width, tex.height),
                                new Vector2(0.5f, 0.5f));
                        }
                    }
                    else
                    {
                        Debug.LogWarning("[UIReforge] fast/bar Image not found");
                    }
                }
                else
                {
                    Debug.LogWarning("[UIReforge] fast/bar Transform not found");
                }

                if (slowTr != null)
                {
                    _hpSlowImage = slowTr.GetComponent<Image>();
                    if (_hpSlowImage != null)
                    {
                        _hpSlowImage.type = Image.Type.Filled;
                        _hpSlowImage.fillMethod = Image.FillMethod.Horizontal;
                        _hpSlowImage.fillOrigin = 0;

                        if (_hpSlowImage.sprite == null && _hpSlowImage.mainTexture is Texture2D tex2)
                        {
                            _hpSlowImage.sprite = Sprite.Create(
                                tex2,
                                new Rect(0, 0, tex2.width, tex2.height),
                                new Vector2(0.5f, 0.5f));
                        }
                    }
                    else
                    {
                        Debug.LogWarning("[UIReforge] slow/bar Image not found");
                    }
                }
                else
                {
                    Debug.LogWarning("[UIReforge] slow/bar Transform not found");
                }

                // текст хп
                _hpText = healthTr.GetComponentInChildren<TextMeshProUGUI>(true);
            }

            if (_hpFastImage == null)
                Debug.LogWarning("[UIReforge] Hp fast image not found");
            if (_hpSlowImage == null)
                Debug.LogWarning("[UIReforge] Hp slow image not found");
            if (_hpText == null)
                Debug.LogWarning("[UIReforge] Hp TMP text not found");

            // --------- FOOD СЛОТЫ ---------
            var foodChildren = new List<Transform>();
            foreach (Transform child in root.transform)
            {
                if (child.name.IndexOf("food", StringComparison.OrdinalIgnoreCase) >= 0)
                    foodChildren.Add(child);
            }

            foodChildren.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.Ordinal));
            Debug.Log("[UIReforge] Food children: " +
                      string.Join(", ", foodChildren.ConvertAll(t => t.name)));

            for (int i = 0; i < _slots.Length; i++)
            {
                if (i >= foodChildren.Count)
                {
                    Debug.LogWarning($"[UIReforge] Not enough food children, slot {i} missing");
                    _slots[i] = null;
                    continue;
                }

                Transform slotTr = foodChildren[i];
                Debug.Log($"[UIReforge] Slot {i} bind to '{slotTr.name}'");

                var slot = new FoodSlotUI
                {
                    Root = slotTr.gameObject,
                    Timer = slotTr.GetComponentInChildren<TextMeshProUGUI>(true)
                };

                // свой объект под иконку
                Transform existing = slotTr.Find("UIReforgeIcon");
                Image iconImg;

                if (existing != null)
                {
                    iconImg = existing.GetComponent<Image>() ?? existing.gameObject.AddComponent<Image>();
                }
                else
                {
                    GameObject iconGO = new GameObject(
                        "UIReforgeIcon",
                        typeof(RectTransform),
                        typeof(Image)
                    );
                    iconGO.transform.SetParent(slotTr, false);

                    var rt = iconGO.GetComponent<RectTransform>();
                    rt.anchorMin = Vector2.zero;
                    rt.anchorMax = Vector2.one;
                    rt.offsetMin = Vector2.zero;
                    rt.offsetMax = Vector2.zero;
                    rt.localScale = Vector3.one;

                    iconImg = iconGO.GetComponent<Image>();
                    iconImg.raycastTarget = false;
                    iconImg.preserveAspect = true;
                }

                slot.Icon = iconImg;
                _slots[i] = slot;
            }
        }

        // ---------- ДОСТАЁМ ЕДУ ИЗ Player.Food (если понадобится) ----------

        private static ItemDrop.ItemData GetFoodItem(Player.Food food)
        {
            try
            {
                if (_foodItemField == null)
                {
                    Type t = food.GetType();
                    var fields = t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                    _foodItemField = fields.FirstOrDefault(f => f.FieldType == typeof(ItemDrop.ItemData))
                                     ?? fields.FirstOrDefault(f => typeof(ItemDrop.ItemData).IsAssignableFrom(f.FieldType));

                    if (_foodItemField == null)
                    {
                        Debug.LogError("[UIReforge] Cannot find ItemDrop.ItemData field in Player.Food");
                        return null;
                    }

                    Debug.Log($"[UIReforge] Using Player.Food field '{_foodItemField.Name}' as ItemData");
                }

                object boxed = food;
                return (ItemDrop.ItemData)_foodItemField.GetValue(boxed);
            }
            catch (Exception ex)
            {
                Debug.LogError("[UIReforge] GetFoodItem error: " + ex);
                return null;
            }
        }

        // ---------- ПАТЧ ЗДОРОВЬЯ ----------

        [HarmonyPatch(typeof(Hud), "UpdateHealth")]
        [HarmonyPrefix]
        private static bool UpdateHealthPrefix(Hud __instance, Player player)
        {
            if (!player)
                return true;

            EnsureInit(__instance);
            if (_panel == null)
                return true;

            float hp = player.GetHealth();
            float maxHp = Mathf.Max(1f, player.GetMaxHealth());
            float target = Mathf.Clamp01(hp / maxHp);

            // первый кадр – инициализируем
            if (_fastValue < 0f)
            {
                _fastValue = target;
                _slowValue = target;
            }

            float dt = Time.deltaTime;

            const float damageSlowSpeed = 0.7f; // как быстро ЖЁЛТАЯ догоняет при уроне
            const float healSlowSpeed = 0.1f; // как быстро КРАСНАЯ догоняет при хиле

            if (target < _fastValue)
            {
                // ----- ПОЛУЧАЕМ УРОН -----
                // красная (fast) сразу падает до таргета
                _fastValue = target;
                // жёлтая (slow) медленно опускается
                _slowValue = Mathf.MoveTowards(_slowValue, target, damageSlowSpeed * dt);
            }
            else if (target > _fastValue)
            {
                // ----- ХИЛИМСЯ -----
                // жёлтая полоса сразу прыгает до нового значения
                _slowValue = target;
                // красная медленно поднимается
                _fastValue = Mathf.MoveTowards(_fastValue, target, healSlowSpeed * dt);
            }
            // если target == _fastValue – ничего не делаем

            if (_hpFastImage != null)
                _hpFastImage.fillAmount = _fastValue;

            if (_hpSlowImage != null)
                _hpSlowImage.fillAmount = _slowValue;

            if (_hpText != null)
                _hpText.text = $"{Mathf.RoundToInt(hp)}/{Mathf.RoundToInt(maxHp)}";

            return false; // глушим ванильный UpdateHealth
        }

        // ---------- ПАТЧ ЕДЫ ----------

        [HarmonyPatch(typeof(Hud), "UpdateFood")]
        [HarmonyPrefix]
        private static bool UpdateFoodPrefix(Hud __instance, Player player)
        {
            if (!player)
                return true;

            EnsureInit(__instance);
            if (_panel == null)
                return true;

            List<Player.Food> foods = player.GetFoods();

            for (int i = 0; i < _slots.Length; i++)
            {
                var slot = _slots[i];
                if (slot == null || slot.Root == null)
                    continue;

                // ромб всегда виден
                slot.Root.SetActive(true);

                if (i < foods.Count)
                {
                    Player.Food food = foods[i];

                    // иконка
                    Sprite iconSprite = null;
                    if (food.m_item?.m_shared != null)
                        iconSprite = food.m_item.m_shared.m_icons?[0];

                    if (slot.Icon != null)
                    {
                        slot.Icon.sprite = iconSprite;
                        var c = slot.Icon.color;
                        c.a = iconSprite != null ? 1f : 0f;
                        slot.Icon.color = c;
                        slot.Icon.enabled = iconSprite != null;
                    }

                    // таймер
                    if (slot.Timer != null)
                    {
                        float timeLeft = food.m_time;
                        slot.Timer.text = Mathf.CeilToInt(timeLeft).ToString();
                        slot.Timer.enabled = true;
                    }
                }
                else
                {
                    // пустой слот
                    if (slot.Icon != null)
                    {
                        slot.Icon.sprite = null;
                        var c = slot.Icon.color;
                        c.a = 0f;
                        slot.Icon.color = c;
                        slot.Icon.enabled = false;
                    }

                    if (slot.Timer != null)
                    {
                        slot.Timer.text = "";
                        slot.Timer.enabled = false;
                    }
                }
            }

            return false; // глушим ванильный UpdateFood
        }
    }
}
