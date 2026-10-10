using UnityEngine;
using UnityEngine.UIElements;

namespace RealityPlayground
{

    [DefaultExecutionOrder(32000)]
    public sealed class HideScreenUI : MonoBehaviour
    {
        float nextScan;
        void LateUpdate()
        {
            if (Time.unscaledTime < nextScan) return;
            nextScan = Time.unscaledTime + .25f;
            foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                if (canvas.renderMode != RenderMode.WorldSpace) canvas.gameObject.SetActive(false);
            foreach (var document in FindObjectsByType<UIDocument>(FindObjectsSortMode.None))
                document.gameObject.SetActive(false);
        }
    }
}
