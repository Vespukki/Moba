using SpacetimeDB;

public static partial class Module
{
    

    [Table(Name = "actor", Public = true)]
    public partial struct Actor
    {
        [PrimaryKey, Unique]
        public uint entity_id;
        public string name;

        public float rotation;
        public float current_health;
        public float max_health;
        public Team team;
        public uint attack_range;
        public float attack_speed;
        public float windup_percent; //percentage
        public Timestamp last_attack_time;
        public float health_regen; //per 5 seconds
    }

    [Reducer]
    public static void DoAllActorUpkeep(ReducerContext ctx)
    {
        foreach (var actor in ctx.Db.actor.Iter())
        {
            DoHealthRegen(ctx, actor);
        }
    }

    [Reducer]
    public static void DoHealthRegen(ReducerContext ctx, Actor actor)
    {
        float totalHealthRegen = actor.health_regen;
        foreach (Buff buff in ctx.Db.buff.entity_id.Filter((actor.entity_id)))
        {
            if (ctx.Db.buff_health_regen.buff_id.Find((uint)buff.buff_id) != null)
            {
                totalHealthRegen += buff.value;
            }
        }

        float healthToAdd = (totalHealthRegen / 5f) * (deltaTime.Microseconds / 1_000_000f);

        if (healthToAdd != 0)
        {
            SetActorHealth(ctx, actor, actor.max_health, actor.current_health + healthToAdd);
        }
    }

    [Reducer]
    public static void SetActorHealth(ReducerContext ctx, Actor actor, float newMaxHealth, float newCurrentHealth)
    {
        Log.Info("Changing health");

        if (newMaxHealth < 0) newMaxHealth = 0;

        if (newCurrentHealth < 0) newCurrentHealth = 0;

        else if (newCurrentHealth > newMaxHealth) newCurrentHealth = newMaxHealth;

        Actor newActor = actor;

        newActor.current_health = newCurrentHealth;
        newActor.max_health = newMaxHealth;

        ctx.Db.actor.entity_id.Delete(actor.entity_id);
        ctx.Db.actor.Insert(newActor);
    }
}