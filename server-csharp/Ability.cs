using SpacetimeDB;
using static Module;

public static partial class Module
{
    public const float FIORA_Q_CAST_RANGE = 100000f;
    public const float FIORA_Q_DASH_DIST = 400f;

    [Type]
    public enum AbilityId {BasicAttack, FioraP, FioraQ }

    [Table(Name = "ability", Public = true)]
    public partial struct Ability
    {
        [AutoInc, PrimaryKey]
        public uint ability_instance_id;

        [SpacetimeDB.Index.BTree]
        public uint ability_id;

        public Timestamp ready_time; //time when the ability will be useable again
    }

    [Reducer]
    public static void UseTargetedAbility(ReducerContext ctx, Ability ability, Attacking attack, Entity entity, Actor actor, Entity targetEntity, ActorBaseStats actorBaseStats)
    {
        switch (ability.ability_id)
        {
            case (uint)AbilityId.BasicAttack:
                HandleBasicAttack(ctx, attack, ability, entity, targetEntity, actor, actorBaseStats);
                break;
            case (uint)AbilityId.FioraQ:
                //FioraQ(ctx, ability, entity, actor, position);
                break;
            default:
                break;
        }
    }

    [Reducer]
    public static void FioraQ(ReducerContext ctx, Ability ability, Entity entity, Actor actor, DbVector2 position)
    {
        
    }
}