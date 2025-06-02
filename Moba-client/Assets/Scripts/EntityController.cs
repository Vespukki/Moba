using SpacetimeDB.Types;
using UnityEngine;

public class EntityController : MonoBehaviour
{
    public float lerpTimer = 0;
    [SerializeField] protected float lerpAmount = .01f;
    protected Vector3 lerpTarget;
    protected Vector3 targetPos;
    public uint entityId;

    const float SNAP_DIST = 1000f;

    private Vector3 lastFramePosition;

    protected float velocity;

    private float moveSpeed = 250;

    protected virtual void Start()
    {

    }
    protected virtual void Awake()
    {

    }

    public void UpdateEntity(Entity newEntity)
    {
        lerpTimer = 0;
        lerpTarget = DbPositionToWorldPosition(newEntity.Position, transform.position.y);
    }

    public void Initialize(Entity entity)
    {
        entityId = entity.EntityId;
        lerpTarget = (Vector2)entity.Position;
        lastFramePosition = transform.position;
    }

    protected virtual void Update()
    {
        MovementStep();
    }

    public void MovementStep()
    {
        velocity = (transform.position - lastFramePosition).magnitude;
        lastFramePosition = transform.position;

        lerpTimer += lerpAmount;

        var difference = targetPos - lerpTarget;

        float distance = difference.magnitude;

        float distanceToMove = moveSpeed * Time.deltaTime;

        var direction = difference.normalized;

        Vector3 newPos;
        if (distance <= distanceToMove)
        {
            newPos = targetPos;
        }
        else
        {
            newPos = lerpTarget + distanceToMove * direction;
        }
        lerpTarget = newPos;
        Vector3 transformExtra = distanceToMove * direction;

        if (Vector3.Distance((distanceToMove * direction), lerpTarget) > SNAP_DIST)
        {
            lerpTimer = 1;
        }

        transform.position = Vector3.Lerp(transform.position + transformExtra, lerpTarget, lerpTimer);
    }

    public Vector3 DbPositionToWorldPosition(DbVector2 dbVector, float height)
    {
        return new Vector3(dbVector.X, height, dbVector.Y);
    }
    public DbVector2 WorldPositionToDbPosition(Vector3 vector3)
    {
        return new DbVector2(vector3.x, vector3.z);
    }

}
