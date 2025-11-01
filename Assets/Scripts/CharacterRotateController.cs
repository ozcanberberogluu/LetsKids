using Photon.Pun;
using UnityEngine;

public class CharacterRotateController : MonoBehaviourPun
{
    [Header("Mouse ile Döndürme Ayarlarý")]
    public float rotationSpeed = 5f; // döndürme hýzý
    public Transform rotateTarget;   // modelin döneceði objeye referans

    private bool isDragging = false;
    private float lastMouseX;

    void Start()
    {
        // Eðer model Transform’unu ayrý döndürmek istiyorsan, buraya model referansýný ver
        if (rotateTarget == null)
            rotateTarget = transform;
    }

    void Update()
    {
        if (!photonView.IsMine) return; // sadece kendi karakterini döndür

        // Mouse týklama kontrolü
        if (Input.GetMouseButtonDown(0))
        {
            isDragging = true;
            lastMouseX = Input.mousePosition.x;
        }
        else if (Input.GetMouseButtonUp(0))
        {
            isDragging = false;
        }

        if (isDragging)
        {
            float deltaX = Input.mousePosition.x - lastMouseX;
            lastMouseX = Input.mousePosition.x;

            float rotationAmount = deltaX * rotationSpeed * Time.deltaTime;
            rotateTarget.Rotate(Vector3.up, -rotationAmount, Space.World);
        }
    }
}
