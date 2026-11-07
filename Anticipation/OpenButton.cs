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
            foreach (Image image in clone.GetComponentsInChildren<Image>(true))
            {
                if (image.gameObject != clone)
                {
                    image.sprite = Icon();
                    image.preserveAspect = true;
                }
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

        // A white hourglass on transparent, tinted by the button like the game's own icons.
        private static Sprite Icon()
        {
            if (_icon != null)
                return _icon;
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float a = Coverage(x + 0.5f, y + 0.5f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            _icon = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            return _icon;
        }

        // How much of the pixel at (x, y) the hourglass covers: two caps, an outlined glass,
        // and sand in the lower bulb.
        private static float Coverage(float x, float y)
        {
            float cx = 32f;
            if ((y >= 6 && y <= 11) || (y >= 53 && y <= 58))
                return x >= 12 && x <= 52 ? 1f : 0f;
            if (y < 11 || y > 53)
                return 0f;
            // Half-width of the glass: wide at the caps, a neck in the middle.
            float t = Mathf.Abs(y - 32f) / 21f;
            float half = 3f + 15f * t * t * (3f - 2f * t);
            float d = Mathf.Abs(x - cx);
            if (d > half + 0.5f)
                return 0f;
            float edge = Mathf.Clamp01(half + 0.5f - d);
            if (d >= half - 3.5f)
                return edge;
            // Sand (texture y runs up): the bottom of the lower bulb, and a thin stream from the neck.
            bool sand = y < 22f || (y < 32f && d < 1.2f);
            return sand ? 1f : 0f;
        }
    }
}
