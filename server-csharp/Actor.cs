using SpacetimeDB;
using System;
using System.Diagnostics;

public static partial class Module
{
    

    [Table(Name = "actor", Public = true)]
    public partial struct Actor
    {
        [PrimaryKey, Unique]
        public uint entity_id;
        //public uint statblock_id;
        public float rotation;
        public float current_health;
        public float max_health;
        public Team team;
        public uint attack_range;
        public float attack_speed;
        public float windup_percent; //percentage
        public Timestamp last_attack_time;
    }



    [Reducer]
    public static void SetActorHealth(ReducerContext ctx, uint entityId, float newMaxHealth, float newCurrentHealth)
    {
        Log.Info("Changing health");

        var actor = ctx.Db.actor.entity_id.Find(entityId);

        if (actor == null) return;

        if (newMaxHealth < 0) newMaxHealth = 0;

        if (newCurrentHealth < 0) newCurrentHealth = 0;

        else if (newCurrentHealth > newMaxHealth) newCurrentHealth = newMaxHealth;

        ctx.Db.actor.entity_id.Delete(entityId);
        ctx.Db.actor.Insert(new()
        {
            entity_id = actor.Value.entity_id,
            rotation = actor.Value.rotation,
            current_health = newCurrentHealth,
            max_health = newMaxHealth
        });
    }
}