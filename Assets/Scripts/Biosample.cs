using UnityEngine;

public class BioSample : MonoBehaviour
{
    public int amount = 1;
    public float rotateSpeed = 100f;

    private void Update()
    {
        // Á¦ÀÚ¸® È¸Àü ¿¬Ãâ
        transform.Rotate(Vector3.up * rotateSpeed * Time.deltaTime);
    }

    private void OnTriggerEnter(Collider other)
    {
        // [µğ¹ö±ë] ¹«¾ùÀÌ¶û ºÎµúÇû´ÂÁö ·Î±× Ãâ·Â
        // Debug.Log($"Ä¸½¶¿¡ ´êÀº ¹°Ã¼: {other.name} / ÅÂ±×: {other.tag}");

        if (other.CompareTag("Player"))
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.AddBioSample(amount);
                Debug.Log("¹ÙÀÌ¿À »ùÇÃ È¹µæ!");
            }

            Destroy(gameObject); // ¸ÔÀ¸¸é »ç¶óÁü
        }
    }
}