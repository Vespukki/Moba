using SpacetimeDB.Types;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public class ActorController : EntityController, IHoverable
{
    protected float rotationLerpTarget;
    
    public Material outlineMat;

    public Team team;

    [SerializeField] protected GameObject healthBarPrefab;
    private HealthBar healthBar;
    [SerializeField] Transform healthBarTarget;

    public Animator animator;
    [SerializeField] protected float maxRotation = 10;

    [SerializeField] SkinnedMeshRenderer smr;
    Color originalColor;

    public Transform centerTransform;

    private List<Buff> buffs = new();

    public delegate void BuffChangeDelegate(ActorController actor, Buff buff);

    public static event BuffChangeDelegate OnBuffAdded;
    public static event BuffChangeDelegate OnBuffRemoved;

    public Actor actor;
    public ActorBaseStats baseStats;
    public List<Buff> GetBuffs()
    {
        return buffs;
    }
    public void AddBuff(Buff buff)
    {
        if (buffs.Contains(buff)) return;

        OnBuffAdded?.Invoke(this, buff);
        buffs.Add(buff);
    }

    public void RemoveBuff(Buff buff)
    {
        OnBuffRemoved?.Invoke(this, buff);
        buffs.Remove(buff);
    }

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
        actor = newActor;

        healthBar.UpdateHealth(actor, baseStats);
    }

    public void UpdateActor(Actor oldActor, Actor newActor)
    {
        rotationLerpTarget = newActor.Rotation;
        actor = newActor;

        healthBar.UpdateHealth(newActor, baseStats);
    }

    public async void FlashRed(int msDelay)
    {
        SkinnedMeshRenderer smr = GetComponentInChildren<SkinnedMeshRenderer>();


        smr.material.color = Color.red;

        await Task.Delay(msDelay);

        smr.material.color = originalColor;
    }

    public void Initialize(Entity entity, Actor actor, ActorBaseStats baseStats)
    {
        Debug.Log($"initializing {gameObject.name}'s entity");
        Initialize(entity);
        this.baseStats = baseStats;
        this.actor = actor;
        team = actor.Team;
        transform.position = (Vector2)entity.Position;
        rotationLerpTarget = actor.Rotation;
        healthBar = HealthBarManager.Instance.InitializeHealthBar(healthBarPrefab);
        HealthBarManager.Instance.SetHealthBarPosition(healthBar, healthBarTarget);

        healthBar.UpdateHealth(actor, baseStats);
    }


    public void BeginHover()
    {
        var mats = smr.materials;
        Material[] updatedMaterials = new Material[mats.Length + 1];

        for (int i = 0; i < mats.Length; i++)
        {
            updatedMaterials[i] = mats[i];
        }

        updatedMaterials[mats.Length] = outlineMat;

        smr.materials = updatedMaterials;

    }

    public void EndHover()
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

}
