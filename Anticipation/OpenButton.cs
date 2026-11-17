using System.Collections;
using UI;
using UI.Tooltips;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Anticipation
{
    // An hourglass button in the top-right row (Company, Roster, Switch List...), cloned from
    // the Switch List button so it looks and fades like the rest. It is re-added whenever the
    // game builds a fresh top-right area, e.g. after loading another save.
    internal sealed class OpenButton : MonoBehaviour
    {
        private const string ButtonName = "Anticipation Button";
        private static OpenButton _instance;
        private static Sprite _icon;

        public static void Install()
        {
            if (_instance != null)
                return;
            var host = new GameObject("Anticipation");
            DontDestroyOnLoad(host);
            _instance = host.AddComponent<OpenButton>();
        }

        public static void Remove()
        {
            if (_instance == null)
                return;
            TopRightArea area = FindAnyObjectByType<TopRightArea>();
            Transform button = area == null ? null : FindDeep(area.transform, ButtonName);
            if (button != null)
                Destroy(button.gameObject);
            Destroy(_instance.gameObject);
            _instance = null;
        }

        private void Start() => StartCoroutine(Watch());

        private IEnumerator Watch()
        {
            var wait = new WaitForSecondsRealtime(1f);
            while (true)
            {
                TopRightArea area = FindAnyObjectByType<TopRightArea>();
                if (area != null && FindDeep(area.transform, ButtonName) == null)
                    Add(area);
                yield return wait;
            }
        }

        private static void Add(TopRightArea area)
        {
            Button template = null;
            foreach (Button button in area.GetComponentsInChildren<Button>(true))
            {
                if (button.onClick.GetPersistentEventCount() > 0 && button.onClick.GetPersistentMethodName(0) == nameof(TopRightArea.ClickedSwitchList))
                {
                    template = button;
                    break;
                }
            }
            if (template == null)
            {
                ModEntry.Log?.Error("Anticipation: couldn't find the Switch List button to put the hourglass beside.");
                _instance.StopAllCoroutines();
                return;
            }
            GameObject clone = Instantiate(template.gameObject, template.transform.parent, false);
            clone.name = ButtonName;
            clone.transform.SetSiblingIndex(template.transform.GetSiblingIndex() + 1);
            Button open = clone.GetComponent<Button>();
            // Persistent (Inspector) listeners can't be removed, so replace the whole event.
            open.onClick = new Button.ButtonClickedEvent();
            open.onClick.AddListener(new UnityAction(AnticipationWindow.Toggle));
            // The icon is the last image in the button: the button's own image when it has no
            // separate background, so the root can't be skipped.
            Image[] images = clone.GetComponentsInChildren<Image>(true);
            if (images.Length > 0)
            {
                Image icon = images[images.Length - 1];
                icon.sprite = Icon();
                icon.overrideSprite = null;
                icon.preserveAspect = true;
            }
            foreach (UITooltipProvider tip in clone.GetComponentsInChildren<UITooltipProvider>(true))
                tip.DynamicTooltipInfo = () => new TooltipInfo("Anticipation", "Cars off the railroad, and when to expect them back.");
        }

        private static Transform FindDeep(Transform root, string name)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == name)
                    return child;
            }
            return null;
        }

        // An hourglass, drawn in the cream of the game's own icons (their colour is in their
        // pictures, not the button's tint).
        private static Sprite Icon()
        {
            if (_icon != null)
                return _icon;
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // 4 x 4 samples a pixel for smooth edges. Texture rows run up; the drawing's run down.
                    int hits = 0;
                    for (int i = 0; i < 4; i++)
                    {
                        for (int j = 0; j < 4; j++)
                        {
                            float px = x + (i + 0.5f) / 4f, py = size - y - (j + 0.5f) / 4f;
                            if (Covered(56f + (px - 32f) / Scale, 65.5f + (py - 32f) / Scale))
                                hits++;
                        }
                    }
                    pixels[y * size + x] = new Color32(220, 212, 200, (byte)(hits * 255 / 16));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            _icon = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            return _icon;
        }

        // The drawing is 112 x 131 units, centred on (56, 65.5), y down; this fits it to the 64 px icon.
        private const float Scale = 0.52f;

        // Whether the point (u, v) of the drawing is solid: a cap at top and bottom, the glass
        // between them pinched to a neck, hollow above the neck, with sand falling in a thin
        // stream onto a pile in the lower bulb.
        private static bool Covered(float u, float v)
        {
            if (Cap(u, v, 10f, 19f) || Cap(u, v, 112f, 121f))
                return true;
            if (v < 18f || v > 113f)
                return false;
            float t = Mathf.Abs(v - 65.5f) / 46.5f;
            float half = 17f + 17f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.1f, 0.8f, t));
            float d = Mathf.Abs(u - 56f);
            if (d >= half)
                return false;
            float inner = half - 9f;
            if (v >= 32f && v <= 65.5f && d < inner)
                return false;
            if (v >= 62f && v <= 96f && d < 2f)
                return false;
            if (v >= 90f && v <= 110f && d < Mathf.Min((v - 90f) / 20f * 26f, inner))
                return false;
            return true;
        }

        private static bool Cap(float u, float v, float top, float bottom)
        {
            const float r = 4.5f;
            float cu = Mathf.Clamp(u, 10f + r, 102f - r), cv = Mathf.Clamp(v, top + r, bottom - r);
            return (u - cu) * (u - cu) + (v - cv) * (v - cv) <= r * r;
        }
    }
}
