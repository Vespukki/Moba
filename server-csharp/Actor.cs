using SpacetimeDB;

public static partial class Module
{
    [Type]
    public enum ActorId { Fiora, Dummy }

    [Table(Name = "actor", Public = true)]
    public partial struct Actor
    {
        [PrimaryKey, Unique]
        public uint entity_id;
        public string name;
        public Team team;
        public Timestamp last_attack_time;
        public float rotation;
        public uint actor_id;

        public float current_health;
        /*public float bonus_health;

        public float bonus_attack_damage;
        public float bonus_armor;
        public float bonus_magic_resist;
        public float bonus_attack_speed_percentage;
        public float bonus_move_speed;
        public float bonus_health_regen_percentage;*/
    }

    [Table(Name = "actor_base_stats", Public = true)]
    public partial struct ActorBaseStats()
    {
        [PrimaryKey]
        public uint actor_id;

        public float max_health;
        public float attack;
        public float armor;
        public float magic_resist;
        public float attack_speed;
        public float windup_percent;
        public float move_speed;
        public float health_regen;
        public float attack_range;

    }

    [Reducer]
    public static void DoAllActorUpkeep(ReducerContext ctx)
    {
        foreach (var actor in ctx.Db.actor.Iter())
        {
            DoHealthRegen(ctx, actor);
        }
    }

    public static float GetHealthRegen(Actor actor, ActorBaseStats baseStats)
    {
        return baseStats.health_regen;
    }

    public static float GetMaxHealth(Actor actor, ActorBaseStats baseStats)
    {
        return baseStats.max_health;
    }

    [Reducer]
    public static void DoHealthRegen(ReducerContext ctx, Actor actor)
    {
        var nBaseStats = ctx.Db.actor_base_stats.actor_id.Find((uint)actor.actor_id);
        if (nBaseStats == null) return;
        float totalHealthRegen = nBaseStats.Value.health_regen;
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
            SetActorHealth(ctx, actor, nBaseStats.Value, actor.current_health + healthToAdd);
        }
    }

    [Reducer]
    public static void SetActorHealth(ReducerContext ctx, Actor actor, ActorBaseStats baseStats, float newCurrentHealth)
    {

        float maxHealth = GetMaxHealth(actor, baseStats);

        if (newCurrentHealth < 0) newCurrentHealth = 0;

        else if (newCurrentHealth > maxHealth) newCurrentHealth = maxHealth;

        Actor newActor = actor;

        newActor.current_health = newCurrentHealth;

        Log.Info($"health difference: {actor.current_health - newActor.current_health}");
        Log.Info($"newActors health: {newActor.current_health}");

        ctx.Db.actor.entity_id.Delete(actor.entity_id);
        var insertedActor = ctx.Db.actor.Insert(newActor);

        Log.Info($"newActors health after insert: {insertedActor.current_health}");

    }
}