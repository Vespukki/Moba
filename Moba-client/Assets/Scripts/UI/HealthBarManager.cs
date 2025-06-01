using SpacetimeDB.Types;
using UnityEngine;

public class HealthBarManager : MonoBehaviour
{
    public static HealthBarManager Instance;
    public Vector3 offset;

    [SerializeField] Canvas canvas;
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Debug.LogError("Too many HealthBarManagers");
        }
    }

    public void SetHealthBarPosition(HealthBar healthBar, Transform healthBarTarget)
    {
        healthBar.parentTransform.position = Camera.main.WorldToScreenPoint(healthBarTarget.position + offset);
    }

    public HealthBar InitializeHealthBar(GameObject barPrefab)
    {
        HealthBar final = Instantiate(barPrefab, canvas.transform).GetComponentInChildren<HealthBar>();

        Debug.Log($"Healthbar spawned {final != null}");
        return final;
    }
}
