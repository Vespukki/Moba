using SpacetimeDB.Types;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public class ActorController : EntityController, IHoverable
{
    protected float rotationLerpTarget;
    
    public Material highlightMat;
    public Material selectMat;

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

    public void BeginSelect()
    {
        var mats = new List<Material>(smr.sharedMaterials);
        if (!mats.Contains(selectMat))
        {
            mats.Add(selectMat);
            smr.sharedMaterials = mats.ToArray();
        }
    }

    public void EndSelect()
    {
        var mats = new List<Material>(smr.sharedMaterials);
        if (mats.Remove(selectMat)) // removes only the selectMat if it exists
        {
            smr.sharedMaterials = mats.ToArray();
        }
    }

    public void BeginHover()
    {
        var mats = new List<Material>(smr.sharedMaterials);
        if (!mats.Contains(highlightMat))
        {
            mats.Add(highlightMat);
            smr.sharedMaterials = mats.ToArray();
        }
    }

    public void EndHover()
    {
        var mats = new List<Material>(smr.sharedMaterials);
        if (mats.Remove(highlightMat)) // removes only the highlightMat if it exists
        {
            smr.sharedMaterials = mats.ToArray();
        }
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

    internal float GetAttackDamage()
    {
        return baseStats.Attack;
    }

    internal float GetAbilityPower()
    {
        return 0;
    }

    internal float GetArmor()
    {
        return baseStats.Armor;
    }

    internal float GetMagicResist()
    {
        return baseStats.MagicResist;
    }

    internal float GetAttackSpeed()
    {
        return baseStats.AttackSpeed;
    }

    internal float GetAbilityHaste()
    {
        return 0;
    }

    internal float GetCritChance()
    {
        return 0;
    }

    internal float GetMoveSpeed()
    {
        return baseStats.MoveSpeed;
    }
}
