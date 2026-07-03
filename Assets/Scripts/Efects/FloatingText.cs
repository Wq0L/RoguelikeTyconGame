using UnityEngine;
using TMPro;

public class FloatingText : MonoBehaviour
{
    [SerializeField] private TextMeshPro textMesh;
    [SerializeField] private float lifetime = 1f;
    [SerializeField] private float moveSpeed = 2f;

    private float timer;
    private Color startColor;
    private Transform cam;

    public void Show(int damage, bool isCrit)
    {
        textMesh.text = damage.ToString();

        if (isCrit)
        {
            textMesh.color = Color.red;
            textMesh.fontSize = 8f;   // crit büyük
        }
        else
        {
            textMesh.color = Color.white;
            textMesh.fontSize = 5f;
        }

        startColor = textMesh.color;
        timer = 0f;
        cam = Camera.main.transform;
        gameObject.SetActive(true);
    }

    private void Update()
    {
        timer += Time.deltaTime;

        // Yukarı kay
        transform.position += Vector3.up * moveSpeed * Time.deltaTime;

        // Kameraya bak (billboard)
        if (cam != null)
            transform.forward = cam.forward;

        // Solma
        float alpha = 1f - (timer / lifetime);
        textMesh.color = new Color(startColor.r, startColor.g, startColor.b, alpha);

        // Süre bitti — pool'a dön
        if (timer >= lifetime)
            VFXManager.Instance.ReturnText(this);
    }
}