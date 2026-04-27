using System.Collections;
using UnityEngine;

/// <summary>
/// Manages the bathroom bad-path scenario in the Illusion Room.
///
/// FLOW:
///   Phase 0 — Player enters bathroom:
///     • Voices stop immediately (quiet refuge)
///     • Anxiety pauses (no increase)
///     • 30-second safe window
///
///   Phase 1 — After 30 seconds:
///     • Door starts knocking + shaking
///     • Voices become LOUDER than before (muffled through door)
///     • Anxiety increases FAST (anxietyPerSecondAfterShake)
///
///   Phase 2 — Anxiety hits panic threshold:
///     • Panic attack fires via PanicAttackController
///     • After panic ends → door stops shaking, player can exit
///
///   On Exit:
///     • Voices return to normal bedroom level
///     • IllusionRoomManager resumes normal anxiety increase
///
/// SETUP:
///   1. Attach to a trigger collider covering the bathroom floor (isTrigger = true).
///   2. Player GameObject must have the "Player" tag.
///   3. Assign all references in the Inspector.
/// </summary>
public class BathroomManager : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The bathroom door Transform — used for the shaking effect.")]
    [SerializeField] private Transform bathroomDoorTransform;

    [Tooltip("The DoorController on the bathroom door — closed and locked on entry, unlocked after panic.")]
    [SerializeField] private DoorController bathroomDoor;

    [Tooltip("AudioSource for the door knocking/banging sound (loops).")]
    [SerializeField] private AudioSource doorKnockSource;

    [Header("Safe Window")]
    [Tooltip("Seconds of quiet refuge before the knocking starts.")]
    [SerializeField] private float safeWindowDuration = 30f;

    [Tooltip("Seconds after the player enters before the door closes behind them.")]
    [SerializeField] private float doorCloseDelay = 1.5f;

    [Header("Shake Settings")]
    [Tooltip("Max door shake offset in world units.")]
    [SerializeField] private float shakeMagnitude = 0.02f;

    [Tooltip("How fast the door shakes (Hz).")]
    [SerializeField] private float shakeSpeed = 25f;

    [Header("Anxiety — Fast Increase Phase")]
    [Tooltip("Anxiety added per second AFTER knocking starts (much faster than normal).")]
    [SerializeField] private float anxietyPerSecondAfterShake = 15f;

    [Tooltip("Anxiety level at which panic attack fires (~85-90).")]
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

        Debug.Log($"[DEBUG][Bathroom] Start. doorTransform={(bathroomDoorTransform!=null?bathroomDoorTransform.name:"NULL")}, " +
                  $"doorKnockSource={(doorKnockSource!=null?"OK":"NULL")}, " +
                  $"safeWindow={safeWindowDuration}s, panicThreshold={panicTriggerAnxiety}");
    }

    void Update()
    {
        if (_shakeActive && bathroomDoorTransform != null)
        {
            float x = Mathf.Sin(Time.time * shakeSpeed)             * shakeMagnitude;
            float y = Mathf.Sin(Time.time * shakeSpeed * 0.7f + 1f) * shakeMagnitude * 0.5f;
            bathroomDoorTransform.localPosition = _doorOriginalPos + new Vector3(x, y, 0f);
        }
    }

    // ─── Trigger Detection ────────────────────────────────────────────────────

    void OnTriggerEnter(Collider other)
    {
        // Use root — the 'Body' child mesh is Untagged; the Player root holds the tag.
        if (!other.transform.root.CompareTag("Player")) return;
        if (_playerInBathroom) return;

        _playerInBathroom = true;

        bool illusionActive = IllusionRoomManager.Instance != null && IllusionRoomManager.Instance.IsIllusionActive;
        Debug.Log($"[DEBUG][Bathroom] Player entered. Door will close in {doorCloseDelay}s. IllusionRoomManager={(IllusionRoomManager.Instance!=null?"OK":"NULL")}, IsIllusionActive={illusionActive}");

        IllusionRoomManager.Instance?.OnPlayerEntersBathroom();

        _bathroomRoutine = StartCoroutine(BathroomRoutine());
        Debug.Log("[DEBUG][Bathroom] Coroutine started — 30s safe window begins.");
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.transform.root.CompareTag("Player")) return;

        _playerInBathroom = false;
        Debug.Log("[DEBUG][Bathroom] Player exited bathroom.");

        if (_bathroomRoutine != null) { StopCoroutine(_bathroomRoutine); _bathroomRoutine = null; }
        StopShaking();

        IllusionRoomManager.Instance?.OnPlayerExitsBathroom();
    }

    // ─── Main Routine ─────────────────────────────────────────────────────────

    IEnumerator BathroomRoutine()
    {
        // Brief delay so the player is fully inside before door closes
        yield return new WaitForSeconds(doorCloseDelay);
        if (bathroomDoor != null) { bathroomDoor.ForceClose(); bathroomDoor.Lock(); }
        Debug.Log("[DEBUG][Bathroom] Door closed and locked.");

        // Safe window (remaining time after door delay)
        float remaining = Mathf.Max(0f, safeWindowDuration - doorCloseDelay);
        Debug.Log($"[DEBUG][Bathroom] Safe window: {remaining}s remaining.");

        if (!_playerInBathroom) { Debug.Log("[DEBUG][Bathroom] Player left during safe window — routine ends."); yield break; }

        // Wait out the remaining safe window
        yield return new WaitForSeconds(remaining);

        if (!_playerInBathroom) { Debug.Log("[DEBUG][Bathroom] Player left during safe window — routine ends."); yield break; }

        // Phase 1
        Debug.Log("[DEBUG][Bathroom] Safe window OVER — starting knocking phase!");
        StartShaking();
        IllusionRoomManager.Instance?.OnBathroomKnockingStarts();

        while (_playerInBathroom)
        {
            if (AnxietyManager.Instance != null && !AnxietyManager.Instance.IsPanicActive)
            {
                AnxietyManager.Instance.AddAnxiety(anxietyPerSecondAfterShake * Time.deltaTime);

                // Log anxiety every second
                if (Mathf.FloorToInt(Time.time) != Mathf.FloorToInt(Time.time - Time.deltaTime))
                    Debug.Log($"[DEBUG][Bathroom] Knocking phase — Anxiety={AnxietyManager.Instance.AnxietyValue:F1}/{panicTriggerAnxiety}");
            }

            if (!_panicFired && AnxietyManager.Instance != null &&
                AnxietyManager.Instance.AnxietyValue >= panicTriggerAnxiety)
            {
                _panicFired = true;
                Debug.Log("[DEBUG][Bathroom] Panic threshold reached! Triggering panic attack.");
                AnxietyManager.Instance.TriggerPanicAttack();

                yield return new WaitUntil(() =>
                    AnxietyManager.Instance == null || !AnxietyManager.Instance.IsPanicActive);

                // Panic over — unlock the door so the player can escape
                if (bathroomDoor != null) bathroomDoor.Unlock();
                StopShaking();
                Debug.Log("[DEBUG][Bathroom] Panic over — door unlocked, shake stopped.");
            }

            yield return null;
        }
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    void StartShaking()
    {
        _shakeActive = true;
        if (doorKnockSource != null && !doorKnockSource.isPlaying)
            doorKnockSource.Play();
    }

    void StopShaking()
    {
        _shakeActive = false;
        if (bathroomDoorTransform != null)
            bathroomDoorTransform.localPosition = _doorOriginalPos;
        if (doorKnockSource != null) doorKnockSource.Stop();
    }
}
