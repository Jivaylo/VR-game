using TMPro;
using UnityEngine;

namespace Garden
{
    public sealed class Question : MonoBehaviour
    {
        public Loop loop;
        public TMP_Text label;
        void Update() { if (loop && label) label.text = loop.AngelQuestion; }
    }
}
