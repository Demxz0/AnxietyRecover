using UnityEngine;

/// <summary>
/// Attaches to the phone GameObject and makes it shake slightly while the phone is ringing.
/// The shake stops as soon as the ring AudioSource stops playing.
///
/// SETUP:
///   1. Attach this script to the phone GameObject (or its parent).
///   2. Assign 'phoneRingSource' — the same AudioSource used by DependencyRoomManager for ringing.
///   3. Optionally tune shakeMagnitude and shakeSpeed.
/// </summary>
public class PhoneRingShake : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The AudioSource that plays the phone ring. The shake is active while this is playing.")]
    [SerializeField] private AudioSource phoneRingSource;

    [Header("Shake Settings")]
    [Tooltip("Max offset in world units for the shake.")]
    [SerializeField] private float shakeMagnitude = 0.005f;

    [Tooltip("How fast the phone vibrates (Hz — higher = faster).")]
    [SerializeField] private float shakeSpeed = 30f;

    private Vector3 _originalLocalPosition;

    void Start()
    {
        _originalLocalPosition = transform.localPosition;

        // Auto-find ring source from DependencyRoomManager if not assigned
        if (phoneRingSource == null && DependencyRoomManager.Instance != null)
        {
            // DependencyRoomManager doesn't expose the ring source directly,
            // so we try to find it on nearby AudioSources
            AudioSource[] sources = GetComponentsInChildren<AudioSource>(true);
            if (sources.Length > 0)
                phoneRingSource = sources[0];
        }
    }

    void Update()
    {
        bool ringing = phoneRingSource != null && phoneRingSource.isPlaying;

        if (ringing)
        {
            float x = Mathf.Sin(Time.time * shakeSpeed)              * shakeMagnitude;
            float y = Mathf.Sin(Time.time * shakeSpeed * 0.7f + 1f)  * shakeMagnitude * 0.5f;
            transform.localPosition = _originalLocalPosition + new Vector3(x, y, 0f);
        }
        else
        {
            // Snap back to original position when not ringing
            if (transform.localPosition != _originalLocalPosition)
                transform.localPosition = _originalLocalPosition;
        }
    }
}
