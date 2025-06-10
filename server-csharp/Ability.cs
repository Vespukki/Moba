using SpacetimeDB;

[Type]
public enum AbilityId { None, BasicAttack, FioraP, FioraQ }

public static partial class Module
{
    public const float FIORA_Q_CAST_RANGE = 100000f;
    public const float FIORA_Q_DASH_DIST = 400f;

   

    [Table(Name = "ability", Public = true)]
    public partial struct Ability
    {
        [AutoInc, PrimaryKey]
        public uint ability_instance_id;

        public AbilityId ability_id;

        public Timestamp ready_time; //time when the ability will be useable again
    }

    [Reducer]
    public static void UseTargetedAbility(ReducerContext ctx, Ability ability, Attacking attack, Entity entity, Actor actor, Entity targetEntity, ActorBaseStats actorBaseStats)
    {
        Log.Info("Using targeted ability");
        switch (ability.ability_id)
        {
            case AbilityId.BasicAttack:
                HandleBasicAttack(ctx, attack, ability, entity, targetEntity, actor, actorBaseStats);
                break;
            case AbilityId.FioraQ:
                FioraQ(ctx, attack, ability, entity, targetEntity, actor, actorBaseStats);
                break;
            default:
                break;
        }
    }

    [Reducer]
    public static void FioraQ(ReducerContext ctx, Attacking attack, Ability ability, Entity entity, Entity targetEntity, Actor actor, ActorBaseStats actorBaseStats)
    {
        if (ability.ready_time < ctx.Timestamp)
        {
            Log.Info("FIORA Q USED");
            Ability newAbility = ability;
            newAbility.ready_time = new(ctx.Timestamp.MicrosecondsSinceUnixEpoch + (long)(8f * 1_000_000f));

            ctx.Db.ability.ability_instance_id.Delete(ability.ability_instance_id);
            ctx.Db.ability.Insert(newAbility);
        }
        
    }
}