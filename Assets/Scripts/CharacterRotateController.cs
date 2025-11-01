using Photon.Pun;
using UnityEngine;

public class CharacterRotateController : MonoBehaviourPun
{
    [Header("Mouse ile D�nd�rme Ayarlar�")]
    public float rotationSpeed = 5f; // d�nd�rme h�z�
    public Transform rotateTarget;   // modelin d�nece�i objeye referans

    private bool isDragging = false;
    private float lastMouseX;

    void Start()
    {
        // E�er model Transform�unu ayr� d�nd�rmek istiyorsan, buraya model referans�n� ver
        if (rotateTarget == null)
            rotateTarget = transform;
    }

    void Update()
    {
        if (!photonView.IsMine) return; // sadece kendi karakterini d�nd�r

        // Mouse t�klama kontrol�
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
