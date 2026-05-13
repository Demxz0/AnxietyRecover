using UnityEngine;
using TMPro;

#if UNITY_EDITOR
using UnityEditor;
#endif

[RequireComponent(typeof(TMP_Text))]
[ExecuteAlways] // Runs in edit mode too
public class ArabicText : MonoBehaviour
{
    [Header(">>> Type your Arabic text HERE (not in TMP field) <<<")]
    [TextArea(3, 8)]
    public string rawArabicText = "";

    [Header("Settings")]
    public bool showTashkeel = false;
    public bool useHinduNumbers = false;

    // Called in edit mode AND play mode whenever something changes
    void OnValidate()
    {
        // Delay one frame to avoid "SendMessage cannot be called during Awake"
        #if UNITY_EDITOR
        EditorApplication.delayCall += ApplyText;
        #endif
    }

    void Awake()
    {
        if (!Application.isPlaying) return;
        ApplyText();
    }

    void Start()
    {
        ApplyText(); // safety net
    }

    public void ApplyText()
    {
        if (this == null) return; // guard for delayCall after destroy
        TMP_Text tmp = GetComponent<TMP_Text>();
        if (tmp == null || string.IsNullOrEmpty(rawArabicText)) return;

        tmp.text = FixArabicParagraph(rawArabicText);
        tmp.isRightToLeftText = false; // Do NOT use TMP's built-in RTL — it conflicts with ArabicFixer
    }

    static string FixArabicParagraph(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;

        string normalised = text.Replace("\r\n", "\n").Replace("\r", "\n");
        string[] lines = normalised.Split('\n');

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        for (int i = 0; i < lines.Length; i++)
        {
            if (i > 0) sb.Append('\n');
            sb.Append(FixLine(lines[i]));
        }
        return sb.ToString();
    }

    static string FixLine(string line)
    {
        if (string.IsNullOrEmpty(line))
            return ArabicSupport.ArabicFixer.Fix(line, false, false);

        // All sentence-ending delimiters to split on
        string[] delimiters = new[] { ". ", "، ", "! ", "؟ ", "، " };

        var segments = SplitKeepingDelimiters(line, delimiters);

        if (segments.Count <= 1)
            return ArabicSupport.ArabicFixer.Fix(line, false, false);

        // Fix each segment's Arabic shaping individually
        string[] fixedSegments = new string[segments.Count];
        for (int i = 0; i < segments.Count; i++)
            fixedSegments[i] = ArabicSupport.ArabicFixer.Fix(segments[i].text, false, false);

        // REVERSE sentence order so TMP's LTR layout = correct RTL reading order
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        for (int i = fixedSegments.Length - 1; i >= 0; i--)
        {
            sb.Append(fixedSegments[i]);
            if (i > 0)
                sb.Append(segments[i - 1].delimiter);
        }
        return sb.ToString();
    }

    struct Segment { public string text; public string delimiter; }

    static System.Collections.Generic.List<Segment> SplitKeepingDelimiters(string input, string[] delimiters)
    {
        var result = new System.Collections.Generic.List<Segment>();
        int start = 0;

        while (start < input.Length)
        {
            int earliest = -1;
            string usedDelim = null;

            foreach (var d in delimiters)
            {
                int idx = input.IndexOf(d, start, System.StringComparison.Ordinal);
                if (idx >= 0 && (earliest < 0 || idx < earliest))
                {
                    earliest = idx;
                    usedDelim = d;
                }
            }

            if (earliest < 0)
            {
                result.Add(new Segment { text = input.Substring(start), delimiter = "" });
                break;
            }

            result.Add(new Segment { text = input.Substring(start, earliest - start), delimiter = usedDelim });
            start = earliest + usedDelim.Length;
        }

        return result;
    }

    public void SetText(string arabicText)
    {
        rawArabicText = arabicText;
        ApplyText();
    }
}