using UnityEngine;
using TMPro; // TextMeshPro를 사용해야 선명합니다.

public class DamagePopup : MonoBehaviour
{
    private TextMeshPro textMesh;
    private Color textColor;
    private Transform mainCamTransform;

    private float disappearTimer;
    private float moveSpeed = 2f;
    private float disappearSpeed = 3f;

    private void Awake()
    {
        textMesh = GetComponent<TextMeshPro>();
        if (Camera.main != null) mainCamTransform = Camera.main.transform;
    }

    public void Setup(int damageAmount)
    {
        textMesh.SetText(damageAmount.ToString());
        textColor = textMesh.color;
        disappearTimer = 0.5f; // 0.5초 뒤부터 흐려지기 시작

        // 텍스트가 항상 카메라를 바라보게 설정 (빌보드)
        // 아이소메트릭 뷰에서는 45도로 누워있기 때문에 이게 중요합니다.
        if (mainCamTransform != null)
        {
            transform.rotation = mainCamTransform.rotation;
        }
    }

    private void Update()
    {
        // 1. 위로 이동
        transform.position += Vector3.up * moveSpeed * Time.deltaTime;

        // 2. 사라지는 타이머
        disappearTimer -= Time.deltaTime;
        if (disappearTimer < 0)
        {
            // 3. 페이드 아웃 (투명해짐)
            textColor.a -= disappearSpeed * Time.deltaTime;
            textMesh.color = textColor;

            // 완전히 투명해지면 삭제
            if (textColor.a < 0)
            {
                Destroy(gameObject);
            }
        }
    }
}