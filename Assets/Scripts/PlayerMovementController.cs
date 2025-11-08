using Photon.Pun;
using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PhotonView))]
public class PlayerMovementController : MonoBehaviourPun
{
    [Header("Hareket")]
    public bool MoveEnabled = false;     // <<< hareket aktif/pasif kontrolü
    public float moveSpeed = 3.8f;
    public float acceleration = 16f;
    public float turnSpeed = 1080f;
    public float gravity = -20f;

    [Header("Dönüş (Network uyumlu)")]
    public bool rotateRoot = true;
    public bool rotateCharactersChild = false;
    Transform charactersRoot;

    [Header("Kamera Orbit (RMB)")]
    public Transform cameraPivot;
    public Camera localCamera;
    public float mouseSensitivity = 120f;
    public float minPitch = -35f;
    public float maxPitch = 65f;

    [Header("Kamera Zoom (Scroll Wheel)")]
    public bool enableZoom = true;
    public float minZoomDistance = 2.0f;
    public float maxZoomDistance = 7.5f;
    public float zoomSpeed = 6f;
    public float zoomLerp = 14f;

    [Header("Animasyon")]
    public Animator anim;
    const string P_Speed = "Speed", P_Grounded = "Grounded", P_MoveX = "MoveX", P_MoveY = "MoveY";
    bool hasMoveX, hasMoveY, hasSpeed, hasGrounded;

    CharacterController cc;
    Vector3 planarVel;
    float verticalVel;
    bool isGameScene;
    Transform _lastActiveModel;

    // Kamera açı & zoom
    float yaw, pitch;
    Vector3 camLocalDir = Vector3.back;
    float camTargetDist;
    float camCurrentDist;

    void Awake()
    {
        cc = GetComponent<CharacterController>();
        charactersRoot = transform.Find("Characters");

        if (!cameraPivot) cameraPivot = transform.Find("CameraPivot");
        if (!localCamera && cameraPivot) localCamera = cameraPivot.GetComponentInChildren<Camera>(true);

        // Başlangıç kamera açılarını yakala
        if (cameraPivot)
        {
            var e = cameraPivot.rotation.eulerAngles;
            yaw = e.y; pitch = NormalizePitch(e.x);
        }

        // Zoom başlangıcı
        if (localCamera)
        {
            var lp = localCamera.transform.localPosition;
            if (lp.sqrMagnitude > 0.0001f)
            {
                camLocalDir = lp.normalized;
                camTargetDist = lp.magnitude;
                camCurrentDist = camTargetDist;
            }
        }

        SceneManager.sceneLoaded += OnSceneLoaded;
        isGameScene = SceneManager.GetActiveScene().name == "GameScene";
        MoveEnabled = isGameScene; // <<< sadece GameScene'de true
        ApplyCameraActiveRule();
        CacheAnimatorParams();
    }

    void OnDestroy() => SceneManager.sceneLoaded -= OnSceneLoaded;

    void OnSceneLoaded(Scene s, LoadSceneMode m)
    {
        isGameScene = s.name == "GameScene";
        MoveEnabled = isGameScene; // <<< sadece GameScene aktif
        ApplyCameraActiveRule();
        ApplyZoomImmediate();
    }

    void ApplyCameraActiveRule()
    {
        if (!localCamera) return;
        bool active = photonView.IsMine && isGameScene;
        if (localCamera.gameObject.activeSelf != active)
            localCamera.gameObject.SetActive(active);
    }

    void Update()
    {
        if (!photonView.IsMine) return;

        RebindAnimatorIfNeeded();
        HandleCameraOrbit();
        HandleCameraZoom();
        HandleMovementAndRotation(); // MoveEnabled burada kontrol edilir
        UpdateAnimator();
    }

    // === Kamera Orbit ===
    void HandleCameraOrbit()
    {
        if (!cameraPivot) return;

        if (Input.GetMouseButton(1))
        {
            yaw += Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
            pitch -= Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        }
        cameraPivot.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }

    // === Kamera Zoom ===
    void HandleCameraZoom()
    {
        if (!enableZoom || !localCamera) return;

        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.0001f)
        {
            camTargetDist -= scroll * zoomSpeed;
            camTargetDist = Mathf.Clamp(camTargetDist, minZoomDistance, maxZoomDistance);
        }

        camCurrentDist = Mathf.MoveTowards(camCurrentDist, camTargetDist, zoomLerp * Time.deltaTime);
        localCamera.transform.localPosition = camLocalDir * camCurrentDist;
    }

    void ApplyZoomImmediate()
    {
        if (!localCamera) return;
        camCurrentDist = Mathf.Clamp(camTargetDist, minZoomDistance, maxZoomDistance);
        localCamera.transform.localPosition = camLocalDir * camCurrentDist;
    }

    // === Hareket + Oto-Dönüş ===
    void HandleMovementAndRotation()
    {
        // Eğer hareket devre dışıysa karakter donsun
        float currentSpeed = MoveEnabled ? moveSpeed : 0f;

        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        Vector2 input = new Vector2(h, v);
        if (input.sqrMagnitude > 1f) input.Normalize();

        // Kamera yön referansı
        Vector3 fwd, right;
        if (cameraPivot)
        {
            fwd = cameraPivot.forward; fwd.y = 0f; fwd.Normalize();
            right = cameraPivot.right; right.y = 0f; right.Normalize();
        }
        else
        {
            fwd = transform.forward; fwd.y = 0f; fwd.Normalize();
            right = transform.right; right.y = 0f; right.Normalize();
        }

        Vector3 desiredDir = (fwd * input.y + right * input.x);
        Vector3 desiredVel = desiredDir * currentSpeed;

        planarVel = Vector3.MoveTowards(planarVel, desiredVel, acceleration * Time.deltaTime);

        // Yöne dönme
        Vector3 faceDir = planarVel; faceDir.y = 0f;
        if (faceDir.sqrMagnitude > 0.0001f && currentSpeed > 0f)
        {
            Quaternion target = Quaternion.LookRotation(faceDir.normalized, Vector3.up);

            if (rotateRoot)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, target, turnSpeed * Time.deltaTime);

            if (rotateCharactersChild && charactersRoot)
                charactersRoot.rotation = Quaternion.RotateTowards(charactersRoot.rotation, target, turnSpeed * Time.deltaTime);
        }

        // Yerçekimi
        if (cc.isGrounded && verticalVel < 0f) verticalVel = -2f;
        verticalVel += gravity * Time.deltaTime;

        // Eğer hareket kapalıysa tüm hız sıfır
        Vector3 move = MoveEnabled ? (planarVel + Vector3.up * verticalVel) : Vector3.up * verticalVel;
        cc.Move(move * Time.deltaTime);
    }

    // === Animator ===
    void UpdateAnimator()
    {
        if (!anim) return;

        Vector3 localPlanar = transform.InverseTransformDirection(new Vector3(planarVel.x, 0f, planarVel.z));
        float nx = Mathf.Clamp(localPlanar.x / Mathf.Max(0.0001f, moveSpeed), -1f, 1f);
        float ny = Mathf.Clamp(localPlanar.z / Mathf.Max(0.0001f, moveSpeed), -1f, 1f);

        float spd = new Vector3(planarVel.x, 0f, planarVel.z).magnitude;
        float norm = Mathf.InverseLerp(0f, moveSpeed, spd);

        if (hasMoveX) anim.SetFloat(P_MoveX, nx);
        if (hasMoveY) anim.SetFloat(P_MoveY, ny);
        if (hasSpeed) anim.SetFloat(P_Speed, MoveEnabled ? norm : 0f); // <<< hareket yoksa Speed=0
        if (hasGrounded) anim.SetBool(P_Grounded, cc.isGrounded);
    }

    // === Animator/Model bağlama ===
    void RebindAnimatorIfNeeded()
    {
        var activeModel = GetActiveModel();
        bool changed = activeModel != _lastActiveModel;

        if (anim == null || !anim.gameObject.activeInHierarchy || changed)
        {
            anim = activeModel ? activeModel.GetComponentInChildren<Animator>(true)
                               : GetComponentInChildren<Animator>(true);
            _lastActiveModel = activeModel;
            CacheAnimatorParams();
        }
    }

    Transform GetActiveModel()
    {
        var chars = transform.Find("Characters");
        if (!chars) return null;
        foreach (Transform genderRoot in chars)
            foreach (Transform child in genderRoot)
                if (child.gameObject.activeInHierarchy) return child;
        return null;
    }

    void CacheAnimatorParams()
    {
        hasMoveX = hasMoveY = hasSpeed = hasGrounded = false;
        if (!anim) return;
        foreach (var p in anim.parameters)
        {
            if (p.name == P_MoveX && p.type == AnimatorControllerParameterType.Float) hasMoveX = true;
            if (p.name == P_MoveY && p.type == AnimatorControllerParameterType.Float) hasMoveY = true;
            if (p.name == P_Speed && p.type == AnimatorControllerParameterType.Float) hasSpeed = true;
            if (p.name == P_Grounded && p.type == AnimatorControllerParameterType.Bool) hasGrounded = true;
        }
    }

    float NormalizePitch(float x) { if (x > 180f) x -= 360f; return x; }
}
