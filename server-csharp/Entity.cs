using SpacetimeDB;

public static partial class Module
{

    [Table(Name = "entity", Public = true)]
    public partial struct Entity
    {
        [PrimaryKey, Unique, AutoInc]
        public uint entity_id;
        public DbVector2 position;
        public DbVector2 last_position;

    }


    [Table(Name = "actor", Public = true)]
    public partial struct Actor
    {
        [PrimaryKey, Unique]
        public uint entity_id;
        //public uint statblock_id;
        public float rotation;
        public float current_health;
        public float max_health;
    }

    [Table(Name = "walking", Public = true)]
    public partial struct Walking
    {
        [PrimaryKey, Unique]
        public uint entity_id;
        public DbVector2 target_walk_pos;
    }

    [Reducer]
    public static void SetTargetWalkPos(ReducerContext ctx, DbVector2 position)
    {
        var player = ctx.Db.player.identity.Find(ctx.Sender) ?? throw new Exception("Player not found");

        foreach (var champ in ctx.Db.champion_instance.player_id.Filter(player.player_id))
        {
            var oldWalking = ctx.Db.walking.entity_id.Find(champ.entity_id);

            if (oldWalking != null)
            {
                var newWalking = new Walking()
                {
                    entity_id = oldWalking.Value.entity_id,
                    target_walk_pos = position
                };

                ctx.Db.walking.entity_id.Delete(champ.entity_id);
                ctx.Db.walking.Insert(newWalking);
            }

            else
            {
                var newWalking = new Walking()
                {
                    entity_id = champ.entity_id,
                    target_walk_pos = position,
                };
                ctx.Db.walking.Insert(newWalking);
            }
        }
    }


    [Reducer]
    public static void ScheduleMoveAllPlayers(ReducerContext ctx, int microsecondDelay)
    {
        var currentTime = ctx.Timestamp;
        var tenSeconds = new TimeDuration { Microseconds = microsecondDelay };
        var futureTimestamp = currentTime + tenSeconds;
        var moveTimer = new MoveAllPlayersTimer
        {
            scheduled_at = new ScheduleAt.Time(futureTimestamp)
        };

        ctx.Db.move_all_players_timer.Insert(moveTimer);
    }

    [Reducer]
    public static void MoveAllPlayers(ReducerContext ctx, MoveAllPlayersTimer timer)
    {
        ScheduleMoveAllPlayers(ctx, TICK_LENGTH_MICROSECONDS);

        deltaTime = ctx.Timestamp.TimeDurationSince(lastTimestamp);

        lastTimestamp = ctx.Timestamp;

        float moveSpeed = 5; //Units per Second

        var list = ctx.Db.walking.Iter();
        foreach (var walker in list)
        {
            //var stats = ctx.Db.champion_stats.champ_id.Find(champ.champ_id);

            var nullableEntity = ctx.Db.entity.entity_id.Find(walker.entity_id);
            if (nullableEntity == null)
            {
                ctx.Db.walking.entity_id.Delete(walker.entity_id);
                continue;
            }
            Entity entity = nullableEntity.Value;

            var nullableUnit = ctx.Db.actor.entity_id.Find(walker.entity_id);
            if (nullableUnit == null)
            {
                ctx.Db.walking.entity_id.Delete(walker.entity_id);
                continue;
            }
            Actor actor = nullableUnit.Value;

            var difference = walker.target_walk_pos - entity.position;

            float distance = difference.Magnitude();

            float distanceToMove = moveSpeed * deltaTime.Microseconds / 1_000_000; //in seconds

            var direction = difference.Normalized();

            float velocity = (entity.last_position - entity.position).Magnitude();

            DbVector2 newPos;
            float finalRotation;
            if (distance <= distanceToMove)
            {
                newPos = walker.target_walk_pos;
                finalRotation = actor.rotation;
                ctx.Db.walking.entity_id.Delete(walker.entity_id);
            }
            else
            {
                newPos = new(entity.position.x + (direction.x * distanceToMove), entity.position.y + (direction.y * distanceToMove));
                float rotationDegrees = MathF.Atan2(direction.x, direction.y) * 180 / MathF.PI;
                finalRotation = rotationDegrees;
            }


            ctx.Db.actor.entity_id.Delete(walker.entity_id);
            ctx.Db.actor.Insert(new Actor()
            {
                entity_id = walker.entity_id,
                rotation = finalRotation,
                current_health = actor.current_health,
                max_health = actor.max_health
            });


            DbVector2 newLastPos = entity.position;

            ctx.Db.entity.entity_id.Delete(walker.entity_id);
            ctx.Db.entity.Insert(new Entity()
            {
                entity_id = walker.entity_id,
                position = newPos,
                last_position = newLastPos,
            });
        }
    }

    [Reducer]
    public static void DoNothing(ReducerContext ctx)
    {

    }

    [Reducer]
    public static void SetEntityHealth(ReducerContext ctx, uint entityId, float newMaxHealth, float newCurrentHealth)
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
