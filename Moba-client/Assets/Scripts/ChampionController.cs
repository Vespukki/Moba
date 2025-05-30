using SpacetimeDB.Types;
using System;
using UnityEngine;

public class ChampionController : MonoBehaviour
{
    int instanceId;
    [SerializeField] private float positionLerpSpeed = 1f;

    Animator animator;

    const float LERP_TIME = .1f;
    const float SNAP_DIST = 1000f;

    [SerializeField] protected float maxRotation = 10;

    private Vector3 lastFramePosition;

    private float lerpTimer = 0;
    [SerializeField] protected float lerpAmount = .01f;
    protected Vector3 lerpTarget;
    protected Vector3 targetPos;

    float rotationLerpTarget;

    private float moveSpeed = 5;

    private void Start()
    {
        animator = GetComponent<Animator>();
    }

    public virtual void Update()
    {
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

        float velocity = (lastFramePosition - transform.position).magnitude;
        if (animator != null)
        {
            animator.SetFloat("Speed", velocity);
        }


        lastFramePosition = transform.position;

        float currentY = transform.eulerAngles.y;
        float rotationDifference = Mathf.DeltaAngle(currentY, rotationLerpTarget);

        float clampedDifference = Mathf.Clamp(rotationDifference, -maxRotation, maxRotation);

        float finalRotation = currentY + clampedDifference;
        transform.rotation = Quaternion.Euler(transform.rotation.eulerAngles.x, finalRotation, transform.rotation.eulerAngles.z);
        //transform.rotation = Quaternion.Euler(transform.rotation.x, Quaternion.LookRotation(direction).eulerAngles.y, transform.rotation.z);
    }

    internal void Initialize(ChampionInstance champ)
    {
        instanceId = champ.InstanceId;
        transform.position = (Vector2)champ.Position;
        lastFramePosition = transform.position;
        lerpTarget = (Vector2)champ.Position;
    }

    public Vector3 DbPositionToWorldPosition(DbVector2 dbVector, float height)
    {
        return new Vector3(dbVector.X, height, dbVector.Y);
    }

    internal void UpdateChampion(ChampionInstance newChamp)
    {
        Debug.Log("Champ update");
        lerpTimer = 0;
        lerpTarget = DbPositionToWorldPosition(newChamp.Position, transform.position.y);
        targetPos = DbPositionToWorldPosition(newChamp.TargetWalkPos, transform.position.y);
        rotationLerpTarget = newChamp.Rotation;
    }
}
