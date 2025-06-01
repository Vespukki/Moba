using SpacetimeDB.Types;
using UnityEngine;

public class ActorController : EntityController
{
    protected float rotationLerpTarget;

    [SerializeField] protected GameObject healthBarPrefab;
    private HealthBar healthBar;
    [SerializeField] Transform healthBarTarget;

    Animator animator;
    [SerializeField] protected float maxRotation = 10;

    protected override void Awake()
    {
        base.Awake();
        animator = GetComponent<Animator>();
    }

    protected override void Start()
    {
        base.Start();
        
    }

    public void UpdateActor(Actor newActor)
    {
        rotationLerpTarget = newActor.Rotation;

        healthBar.UpdateHealth(newActor);
    }

    public void Initialize(Entity entity, Actor actor)
    {
        Initialize(entity);
        transform.position = (Vector2)entity.Position;
        rotationLerpTarget = actor.Rotation;
        healthBar = HealthBarManager.Instance.InitializeHealthBar(healthBarPrefab);
        HealthBarManager.Instance.SetHealthBarPosition(healthBar, healthBarTarget);
        healthBar.UpdateHealth(actor);
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
}
