using UnityEngine;
using TMPro;

[RequireComponent(typeof(TMP_Text))]
public class ArabicText : MonoBehaviour
{
    [Header(">>> Type your Arabic text HERE (not in TMP field) <<<")]
    [TextArea(2, 5)]
    public string rawArabicText = "";

    void Awake()
    {
        if (string.IsNullOrEmpty(rawArabicText)) return;
        ApplyText();
    }

    void ApplyText()
    {
        TMP_Text tmp = GetComponent<TMP_Text>();
        tmp.text = ArabicSupport.ArabicFixer.Fix(rawArabicText, false, false);
    }

    // Call this from any other script to update text at runtime
    public void SetText(string arabicText)
    {
        rawArabicText = arabicText;
        if (string.IsNullOrEmpty(rawArabicText)) return;
        ApplyText();
    }
}
