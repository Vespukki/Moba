using UnityEngine;
using UnityEngine.VFX;

public class VFXObjectRotation : MonoBehaviour
{
    public VisualEffect vfx;

    private void Awake()
    {
        if (vfx == null)
        {
            vfx = GetComponentInChildren<VisualEffect>();
        }
        
    }

    private void Start()
    {
        Vector3 eulerRotation = transform.eulerAngles;
        vfx.SetFloat("ParticleRotation", eulerRotation.y);
    }

    void Update()
    {
        Vector3 eulerRotation = transform.eulerAngles;
        vfx.SetFloat("ParticleRotation", eulerRotation.y);
    }
}