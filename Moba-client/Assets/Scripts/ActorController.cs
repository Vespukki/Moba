using SpacetimeDB.Types;
using System.Threading.Tasks;
using UnityEngine;

public class ActorController : EntityController
{
    protected float rotationLerpTarget;

    public Team team;

    [SerializeField] protected GameObject healthBarPrefab;
    private HealthBar healthBar;
    [SerializeField] Transform healthBarTarget;

    public Animator animator;
    [SerializeField] protected float maxRotation = 10;

    [SerializeField] SkinnedMeshRenderer smr;
    Color originalColor;

    public Transform centerTransform;

    protected override void Awake()
    {
        base.Awake();
        if (centerTransform == null) centerTransform = transform;
        originalColor = smr.material.color;

        animator = GetComponent<Animator>();
    }

    protected override void Start()
    {
        base.Start();
        
    }

    public void InsertActor(Actor newActor)
    {
        rotationLerpTarget = newActor.Rotation;

        healthBar.UpdateHealth(newActor);
    }

    public void UpdateActor(Actor oldActor, Actor newActor)
    {
        rotationLerpTarget = newActor.Rotation;

        healthBar.UpdateHealth(newActor);
        /*if (newActor.CurrentHealth != oldActor.CurrentHealth)
        {
            FlashRed(500);
        }*/
    }

    public async void FlashRed(int msDelay)
    {
        SkinnedMeshRenderer smr = GetComponentInChildren<SkinnedMeshRenderer>();


        smr.material.color = Color.red;

        await Task.Delay(msDelay);

        smr.material.color = originalColor;
    }

    public void Initialize(Entity entity, Actor actor)
    {
        Debug.Log($"initializing {gameObject.name}'s entity");
        Initialize(entity);
        team = actor.Team;
        transform.position = (Vector2)entity.Position;
        rotationLerpTarget = actor.Rotation;
        healthBar = HealthBarManager.Instance.InitializeHealthBar(healthBarPrefab);
        HealthBarManager.Instance.SetHealthBarPosition(healthBar, healthBarTarget);
        healthBar.UpdateHealth(actor);
    }


    public void SetOutline(Material outlineMat)
    {
        Debug.Log("setting outline");

        var mats = smr.materials;
        Material[] updatedMaterials = new Material[mats.Length + 1];

        for (int i = 0; i < mats.Length; i++)
        {
            updatedMaterials[i] = mats[i];
        }

        updatedMaterials[mats.Length] = outlineMat;

        smr.materials = updatedMaterials;

    }

    public void RemoveOutline()
    {
        var mats = smr.materials;
        Material[] updatedMaterials = new Material[mats.Length - 1];

        for (int i = 0; i < mats.Length - 1; i++)
        {
            updatedMaterials[i] = mats[i];
        }

        smr.materials = updatedMaterials;
    }

    protected override void Update()
    {
        base.Update();
        HealthBarManager.Instance.SetHealthBarPosition(healthBar, healthBarTarget);
        if (animator != null)
        {
            animator.SetFloat("Speed", velocity);
        }

        float currentY = transform.eulerAngles.y;
        float rotationDifference = Mathf.DeltaAngle(currentY, rotationLerpTarget);

        float clampedDifference = Mathf.Clamp(rotationDifference, -maxRotation, maxRotation);

        float finalRotation = currentY + clampedDifference;
        transform.rotation = Quaternion.Euler(transform.rotation.eulerAngles.x, finalRotation, transform.rotation.eulerAngles.z);
    }

   /* public void UpdateAttack(Attacking attack)
    {

    }*/
}
