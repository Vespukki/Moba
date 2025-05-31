using SpacetimeDB.Types;
using UnityEngine;

public class ActorController : EntityController
{
    public float maxHealth;
    public float currentHealth;

    protected float rotationLerpTarget;
    

    Animator animator;
    [SerializeField] protected float maxRotation = 10;
    protected override void Start()
    {
        base.Start();
        animator = GetComponent<Animator>();
    }

    public void UpdateActor(Actor newActor)
    {
        rotationLerpTarget = newActor.Rotation;
    }

    public void Initialize(Entity entity, Actor actor)
    {
        Initialize(entity);
        transform.position = (Vector2)entity.Position;
        rotationLerpTarget = actor.Rotation;

    }

    protected override void Update()
    {
        base.Update();

        if (animator != null)
        {
            animator.SetFloat("Speed", velocity);
        }

        float currentY = transform.eulerAngles.y;
        float rotationDifference = Mathf.DeltaAngle(currentY, rotationLerpTarget);

        float clampedDifference = Mathf.Clamp(rotationDifference, -maxRotation, maxRotation);

        float finalRotation = currentY + clampedDifference;
        transform.rotation = Quaternion.Euler(transform.rotation.eulerAngles.x, finalRotation, transform.rotation.eulerAngles.z);
        //transform.rotation = Quaternion.Euler(transform.rotation.x, Quaternion.LookRotation(direction).eulerAngles.y, transform.rotation.z);
    }
}
