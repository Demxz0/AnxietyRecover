using System.Collections;
using UnityEngine;

/// <summary>
/// Manages the bathroom bad-path scenario in the Illusion Room.
///
/// BAD PATH FLOW:
///   1. Player enters bathroom and locks it → doors closed
///   2. After 30s: door starts shaking (audio + position lerp) + voices get louder
///   3. Anxiety hits panic level → PanicAttackController handles panic attack
///   4. After panic ends: door stops shaking, player can open it
///   5. OnExitBathroom → IllusionRoomManager shifts sounds back to bedroom
///
/// SETUP:
///   1. Attach to the Bathroom GameObject (or a child manager object).
///   2. Assign the bathroom door Transform for the shake effect.
///   3. Assign the door entry/exit trigger collider references.
///   4. The bathroom door should use DoorController (no lock needed — player can open freely once shaking stops).
/// </summary>
public class BathroomManager : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The bathroom door Transform — used for the shaking effect.")]
    [SerializeField] private Transform bathroomDoorTransform;

    [Tooltip("AudioSource for the door knocking/banging sound.")]
    [SerializeField] private AudioSource doorKnockSource;

    [Tooltip("Anxiety added per second while hiding in the bathroom.")]
    [SerializeField] private float anxietyPerSecondInBathroom = 8f;

    [Header("Shaking Settings")]
    [Tooltip("Seconds after entering bathroom before shaking starts.")]
    [SerializeField] private float shakeDelay = 30f;

    [Tooltip("Max door shake offset in world units.")]
    [SerializeField] private float shakeMagnitude = 0.02f;

    [Tooltip("How fast the door shakes (Hz).")]
    [SerializeField] private float shakeSpeed = 25f;

    [Header("Anxiety — Panic Trigger")]
    [Tooltip("Anxiety level at which panic attack fires (should be at or above panic threshold ~85).")]
    [SerializeField] private float panicTriggerAnxiety = 90f;

    // ─── State ────────────────────────────────────────────────────────────────
    private bool      _playerInBathroom;
    private bool      _shakeActive;
    private bool      _panicFired;
    private Vector3   _doorOriginalPos;
    private Coroutine _bathroomRoutine;

    void Start()
    {
        if (bathroomDoorTransform != null)
            _doorOriginalPos = bathroomDoorTransform.localPosition;
    }

    void Update()
    {
        if (_shakeActive && bathroomDoorTransform != null)
        {
            float x = Mathf.Sin(Time.time * shakeSpeed)              * shakeMagnitude;
            float y = Mathf.Sin(Time.time * shakeSpeed * 0.7f + 1f)  * shakeMagnitude * 0.5f;
            bathroomDoorTransform.localPosition = _doorOriginalPos + new Vector3(x, y, 0f);
        }
    }

    // ─── Trigger Detection ────────────────────────────────────────────────────
    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        if (_playerInBathroom) return;

        _playerInBathroom = true;
        IllusionRoomManager.Instance?.OnPlayerHidesInBathroom();

        _bathroomRoutine = StartCoroutine(BathroomRoutine());
        Debug.Log("[Bathroom] Player entered bathroom — BAD PATH started.");
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        _playerInBathroom = false;

        if (_bathroomRoutine != null) { StopCoroutine(_bathroomRoutine); _bathroomRoutine = null; }
        StopShaking();

        IllusionRoomManager.Instance?.OnPlayerExitsBathroom();
        Debug.Log("[Bathroom] Player exited bathroom.");
    }

    // ─── Bathroom Routine ─────────────────────────────────────────────────────
    IEnumerator BathroomRoutine()
    {
        float timer = 0f;

        while (_playerInBathroom)
        {
            timer += Time.deltaTime;

            // Continuous anxiety increase
            if (AnxietyManager.Instance != null && !AnxietyManager.Instance.IsPanicActive)
                AnxietyManager.Instance.AddAnxiety(anxietyPerSecondInBathroom * Time.deltaTime);

            // Start shaking after delay
            if (!_shakeActive && timer >= shakeDelay)
            {
                StartShaking();
            }

            // Trigger panic when anxiety is high enough
            if (!_panicFired && AnxietyManager.Instance != null &&
                AnxietyManager.Instance.AnxietyValue >= panicTriggerAnxiety)
            {
                _panicFired = true;
                AnxietyManager.Instance.TriggerPanicAttack();
                Debug.Log("[Bathroom] PANIC ATTACK triggered — bad path consequence!");

                // Wait for panic to end before stopping shake
                yield return new WaitUntil(() =>
                    AnxietyManager.Instance == null || !AnxietyManager.Instance.IsPanicActive);

                StopShaking();
                Debug.Log("[Bathroom] Panic ended — door shake stops, player can exit.");
            }

            yield return null;
        }
    }

    void StartShaking()
    {
        _shakeActive = true;
        if (doorKnockSource != null && !doorKnockSource.isPlaying)
            doorKnockSource.Play();
        Debug.Log("[Bathroom] Door shaking + knocking sounds started!");
    }

    void StopShaking()
    {
        _shakeActive = false;
        if (bathroomDoorTransform != null)
            bathroomDoorTransform.localPosition = _doorOriginalPos;
        if (doorKnockSource != null) doorKnockSource.Stop();
    }
}
