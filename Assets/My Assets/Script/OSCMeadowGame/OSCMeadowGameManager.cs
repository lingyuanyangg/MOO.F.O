using UnityEngine;

public enum OSCMatchWinner
{
    None,
    Cow,
    UFO,
    Alien,
    Draw
}

[DisallowMultipleComponent]
public class OSCMeadowGameManager : MonoBehaviour
{
    [Header("Players")]
    public OSCCowController cow;
    public OSCUFOController ufo;
    public OSCAlienController alien;

    [Header("Match")]
    [Min(5f)] public float matchDurationSeconds = 90f;

    [Header("Winner Camera")]
    public Camera gameplayCamera;
    public float winnerCloseUpDistance = 3.25f;
    public float winnerCameraFov = 38f;
    public float winnerCameraMoveSpeed = 3.6f;

    public int CowScore { get; private set; }
    public int UfoScore { get; private set; }
    public int AlienScore { get; private set; }
    public float RemainingTime { get; private set; }
    public bool IsMatchStarted { get; private set; }
    public bool IsMatchOver { get; private set; }
    public OSCMatchWinner Winner { get; private set; } = OSCMatchWinner.None;

    private Vector3 originalCameraPosition;
    private Quaternion originalCameraRotation;
    private float originalCameraFov;
    private Rigidbody cowBody;
    private Rigidbody ufoBody;
    private Rigidbody alienBody;
    private RigidbodyConstraints cowConstraints;
    private RigidbodyConstraints ufoConstraints;
    private RigidbodyConstraints alienConstraints;
    private bool cowUseGravity;
    private bool ufoUseGravity;
    private bool alienUseGravity;
    private OSCCameraProjectileSpawner projectileSpawner;

    private void Start()
    {
        if (cow == null) cow = FindFirstObjectByType<OSCCowController>();
        if (ufo == null) ufo = FindFirstObjectByType<OSCUFOController>();
        if (alien == null) alien = FindFirstObjectByType<OSCAlienController>();
        if (gameplayCamera == null) gameplayCamera = Camera.main;
        projectileSpawner = FindFirstObjectByType<OSCCameraProjectileSpawner>();

        cowBody = cow != null ? cow.GetComponent<Rigidbody>() : null;
        ufoBody = ufo != null ? ufo.GetComponent<Rigidbody>() : null;
        alienBody = alien != null ? alien.GetComponent<Rigidbody>() : null;
        if (cowBody != null)
        {
            cowConstraints = cowBody.constraints;
            cowUseGravity = cowBody.useGravity;
        }
        if (ufoBody != null)
        {
            ufoConstraints = ufoBody.constraints;
            ufoUseGravity = ufoBody.useGravity;
        }
        if (alienBody != null)
        {
            alienConstraints = alienBody.constraints;
            alienUseGravity = alienBody.useGravity;
        }

        if (gameplayCamera != null)
        {
            originalCameraPosition = gameplayCamera.transform.position;
            originalCameraRotation = gameplayCamera.transform.rotation;
            originalCameraFov = gameplayCamera.fieldOfView;
        }

        PrepareStartScreen();
    }

    private void Update()
    {
        if (!IsMatchStarted || IsMatchOver) return;

        RemainingTime = Mathf.Max(0f, RemainingTime - Time.deltaTime);
        if (RemainingTime <= 0f) EndMatch();
    }

    private void LateUpdate()
    {
        if (!IsMatchOver || gameplayCamera == null ||
            Winner == OSCMatchWinner.None || Winner == OSCMatchWinner.Draw)
            return;

        Transform winnerTransform = Winner == OSCMatchWinner.Cow
            ? cow != null ? cow.transform : null
            : Winner == OSCMatchWinner.UFO
                ? ufo != null ? ufo.transform : null
                : alien != null ? alien.transform : null;
        if (winnerTransform == null) return;

        float focusHeight = Winner == OSCMatchWinner.Cow
            ? 1.05f
            : Winner == OSCMatchWinner.Alien ? 1.2f : 0.15f;
        Vector3 focus = winnerTransform.position + Vector3.up * focusHeight;
        Vector3 viewDirection = originalCameraPosition - focus;
        if (viewDirection.sqrMagnitude < 0.01f) viewDirection = Vector3.back;
        viewDirection.Normalize();

        float distance = Winner == OSCMatchWinner.UFO
            ? winnerCloseUpDistance * 1.18f
            : winnerCloseUpDistance;
        Vector3 desiredPosition =
            focus + viewDirection * distance + Vector3.up * 0.22f;
        Quaternion desiredRotation = Quaternion.LookRotation(
            focus - desiredPosition,
            Vector3.up);

        float blend = 1f - Mathf.Exp(-winnerCameraMoveSpeed * Time.deltaTime);
        gameplayCamera.transform.position = Vector3.Lerp(
            gameplayCamera.transform.position, desiredPosition, blend);
        gameplayCamera.transform.rotation = Quaternion.Slerp(
            gameplayCamera.transform.rotation, desiredRotation, blend);
        gameplayCamera.fieldOfView = Mathf.Lerp(
            gameplayCamera.fieldOfView, winnerCameraFov, blend);
    }

    public void BeginMatch(float requestedDurationSeconds)
    {
        if (IsMatchStarted && !IsMatchOver) return;

        matchDurationSeconds = Mathf.Clamp(
            Mathf.Round(requestedDurationSeconds), 5f, 3600f);
        ResetScores();
        RemainingTime = matchDurationSeconds;
        Winner = OSCMatchWinner.None;
        IsMatchOver = false;
        IsMatchStarted = true;

        RestoreGameplayCamera();
        if (cow != null) cow.ResetForNewMatch();
        if (ufo != null) ufo.ResetForNewMatch();
        if (alien != null) alien.ResetForNewMatch();
        SetGameplayEnabled(true);
    }

    public void PrepareStartScreen()
    {
        ResetScores();
        RemainingTime = Mathf.Clamp(matchDurationSeconds, 5f, 3600f);
        Winner = OSCMatchWinner.None;
        IsMatchStarted = false;
        IsMatchOver = false;
        RestoreGameplayCamera();
        SetGameplayEnabled(false);
    }

    public void AwardCowPoint()
    {
        if (IsMatchStarted && !IsMatchOver) CowScore++;
    }

    public void AwardUfoPoint()
    {
        if (IsMatchStarted && !IsMatchOver) UfoScore++;
    }

    public void AwardAlienPoint()
    {
        if (IsMatchStarted && !IsMatchOver) AlienScore++;
    }

    public void AwardPoint(MonoBehaviour attacker)
    {
        if (attacker is OSCCowController) AwardCowPoint();
        else if (attacker is OSCUFOController) AwardUfoPoint();
        else if (attacker is OSCAlienController) AwardAlienPoint();
    }

    public void ResetScores()
    {
        CowScore = 0;
        UfoScore = 0;
        AlienScore = 0;
    }

    public void ResetMatch()
    {
        if (MeadowTrafficLight.Instance != null) MeadowTrafficLight.Instance.ResetSignal();
        if (cow != null) cow.ResetForNewMatch();
        if (ufo != null) ufo.ResetForNewMatch();
        if (alien != null) alien.ResetForNewMatch();
        PrepareStartScreen();
    }

    public void EndMatchNow()
    {
        if (IsMatchStarted) EndMatch();
    }

    private void EndMatch()
    {
        if (IsMatchOver || !IsMatchStarted) return;

        RemainingTime = 0f;
        IsMatchOver = true;
        int highest = Mathf.Max(CowScore, Mathf.Max(UfoScore, AlienScore));
        int leaders = (CowScore == highest ? 1 : 0) +
                      (UfoScore == highest ? 1 : 0) +
                      (AlienScore == highest ? 1 : 0);
        Winner = leaders != 1
            ? OSCMatchWinner.Draw
            : CowScore == highest
                ? OSCMatchWinner.Cow
                : UfoScore == highest
                    ? OSCMatchWinner.UFO
                    : OSCMatchWinner.Alien;

        SetGameplayEnabled(false);

        foreach (OSCThrownHazard hazard in
                 FindObjectsByType<OSCThrownHazard>(
                     FindObjectsSortMode.None))
        {
            if (hazard != null) Destroy(hazard.gameObject);
        }
    }

    private void SetGameplayEnabled(bool value)
    {
        if (cow != null)
        {
            cow.ResetControlInput();
            if (!value) cow.StopAllCoroutines();
            cow.enabled = value;
            ConfigureBody(cowBody, value, cowConstraints, cowUseGravity);
        }

        if (ufo != null)
        {
            ufo.ResetControlInput();
            if (!value) ufo.StopAllCoroutines();
            ufo.enabled = value;
            ConfigureBody(ufoBody, value, ufoConstraints, ufoUseGravity);
        }

        if (alien != null)
        {
            alien.ResetControlInput();
            if (!value) alien.StopAllCoroutines();
            alien.enabled = value;
            ConfigureBody(
                alienBody, value, alienConstraints, alienUseGravity);
        }

        if (projectileSpawner != null)
            projectileSpawner.enabled = value;
    }

    private static void ConfigureBody(
        Rigidbody body,
        bool gameplayEnabled,
        RigidbodyConstraints gameplayConstraints,
        bool gameplayUseGravity)
    {
        if (body == null) return;
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        body.useGravity = gameplayEnabled && gameplayUseGravity;
        body.constraints = gameplayEnabled
            ? RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ
            : RigidbodyConstraints.FreezeAll;
    }

    private void RestoreGameplayCamera()
    {
        if (gameplayCamera == null) return;
        gameplayCamera.transform.position = originalCameraPosition;
        gameplayCamera.transform.rotation = originalCameraRotation;
        gameplayCamera.fieldOfView = originalCameraFov;
    }
}
