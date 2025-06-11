using SpacetimeDB;

[Type]
public enum AbilityId { None, BasicAttack, FioraP, FioraQ }

public static partial class Module
{
    public const float FIORA_Q_CAST_RANGE = 100000f;
    public const float FIORA_Q_DASH_DIST = 400f;
    public const float FIORA_Q_DASH_SPEED = 400f;

   

    [Table(Name = "ability", Public = true)]
    public partial struct Ability
    {
        [AutoInc, PrimaryKey]
        public uint ability_instance_id;

        public AbilityId ability_id;

        public Timestamp ready_time; //time when the ability will be useable again

        public bool targeted;
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
                FioraQ(ctx, attack, ability, entity, actor, actorBaseStats);
                break;
            default:
                break;
        }
    }

    [Reducer]
    public static void FioraQ(ReducerContext ctx, Attacking attack, Ability ability, Entity entity, Actor actor, ActorBaseStats actorBaseStats)
    {
        Ability newAbility = ability;

        DbVector2 position = attack.target_position;

        switch (attack.attack_state)
        {
            case AttackState.Ready:

                if (ability.ready_time < ctx.Timestamp)
                {
                    Log.Info("FIORA Q USED");

                    newAbility.ready_time = new(ctx.Timestamp.MicrosecondsSinceUnixEpoch + (long)(8f * 1_000_000f));

                    Attacking newAttack = attack;


                    newAttack.attack_start_time = ctx.Timestamp;
                    newAttack.attack_state = AttackState.Starting;


                    ctx.Db.attacking.ability_instance_id.Delete(ability.ability_instance_id);
                    ctx.Db.attacking.Insert(newAttack);

                    goto case AttackState.Starting;
                }

                break;
            case AttackState.Starting:
                //MOVE TOWARDS DASH POS
                #region movement math
                var difference = position - entity.position;

                float distance = difference.Magnitude();

                float distanceToMove = FIORA_Q_DASH_SPEED * (float)deltaTime.Microseconds / 1_000_000f; //in seconds

                var direction = difference.Normalized();

                float velocity = (entity.last_position - entity.position).Magnitude();

                DbVector2 newPos;
                if (distance <= distanceToMove)
                {
                    newPos = position;
                    ctx.Db.attacking.entity_id.Delete(entity.entity_id);
                    Log.Info("Made it to dash pos");
                }
                else
                {
                    Log.Info("Moving to final dash pos");
                    newPos = new(entity.position.x + (direction.x * distanceToMove), entity.position.y + (direction.y * distanceToMove));
                }

                #endregion

                Entity newEntity = entity;
                newEntity.position = newPos;
                newEntity.last_position = entity.position;


                ctx.Db.entity.entity_id.Delete(entity.entity_id);
                ctx.Db.entity.Insert(newEntity);


                break;
            case AttackState.Committed:
                break;
            default:
                break;
        }

        ctx.Db.ability.ability_instance_id.Delete(ability.ability_instance_id);
        ctx.Db.ability.Insert(newAbility);
    }
}