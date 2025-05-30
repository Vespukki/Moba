using SpacetimeDB;
using System.Numerics;

public static partial class Module
{
    public static Timestamp lastTimestamp;
    public static int tickCount;
    public static bool startLogging = false;
    public static TimeDuration deltaTime;

    public const int TICK_LENGTH_MICROSECONDS = 30_000;

    [Table(Name = "player", Public = true)]
    [Table(Name = "logged_out_player", Public = true)]
    public partial struct Player
    {
        [PrimaryKey]
        public Identity identity;
        [Unique, AutoInc]
        public uint player_id;
        public string name;
    }

    [Table(Name = "config")]
    public partial struct Config
    {
        [PrimaryKey, AutoInc, Unique]
        public uint config_id;

        public float tick_length;

        public float GetTicksPerSecond()
        {
            return 1 / tick_length;
        }
    }

    [Table(Name = "champion_stats", Public = true)]
    public partial struct ChampionStats
    {
        [PrimaryKey, Unique]
        public string champ_id;
        public string name;
        public int base_ad;
    }

    [Table(Name = "champion_instance", Public = true)]
    public partial struct ChampionInstance
    {
        [PrimaryKey, Unique, AutoInc]
        public int instance_id;

        public string champ_id;

        [SpacetimeDB.Index.BTree]
        public uint player_id;

        public DbVector2 target_walk_pos;

        public DbVector2 position;
        public DbVector2 last_position;
        public float rotation;

    }

    [Table(Name = "move_all_players_timer", Scheduled = nameof(MoveAllPlayers), ScheduledAt = nameof(scheduled_at))]
    public partial struct MoveAllPlayersTimer
    {
        [PrimaryKey, AutoInc]
        public ulong scheduled_id;
        public ScheduleAt scheduled_at;
    }

    [Reducer]
    public static void ScheduleMoveAllPlayers(ReducerContext ctx,int microsecondDelay)
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
        float moveSpeed = 5; //Units per Second
        deltaTime = ctx.Timestamp.TimeDurationSince(lastTimestamp);

        if (startLogging)
        {
            lastTimestamp = ctx.Timestamp;
            tickCount++;
            Log.Info($"tick {tickCount} in {deltaTime.Microseconds / 1000}ms");

        }
        ScheduleMoveAllPlayers(ctx, TICK_LENGTH_MICROSECONDS);


        var list = ctx.Db.champion_instance.Iter();
        foreach (var champ in list)
        {

            var stats = ctx.Db.champion_stats.champ_id.Find(champ.champ_id);

            var difference = new DbVector2(champ.target_walk_pos.x - champ.position.x, champ.target_walk_pos.y - champ.position.y);

            float distance = difference.Magnitude();

            float distanceToMove = moveSpeed * deltaTime.Microseconds / 1_000_000;

            var direction = difference.Normalized();

            float velocity = (champ.last_position - champ.position).Magnitude();

            DbVector2 newPos;
            float finalRotation;
            if (distance <= distanceToMove)
            {
                newPos = champ.target_walk_pos;
                finalRotation = champ.rotation;
            }
            else
            {
                newPos = new(champ.position.x + (direction.x * distanceToMove), champ.position.y + (direction.y * distanceToMove));
                float rotationDegrees = MathF.Atan2(direction.x, direction.y) * 180 / MathF.PI;
                finalRotation = rotationDegrees;
            }



            

            ctx.Db.champion_instance.instance_id.Delete(champ.instance_id);
            ctx.Db.champion_instance.Insert(new ChampionInstance
            {
                instance_id = champ.instance_id,
                champ_id = champ.champ_id,
                player_id = champ.player_id,
                last_position = champ.position,
                position = newPos,
                rotation = finalRotation,
                target_walk_pos = champ.target_walk_pos
            });

        }
}

    [Reducer]
    public static void SetTargetWalkPos(ReducerContext ctx, DbVector2 position)
    {
        var player = ctx.Db.player.identity.Find(ctx.Sender) ?? throw new Exception("Player not found");

        foreach (var champ in ctx.Db.champion_instance.player_id.Filter(player.player_id))
        {
            // Create an updated copy of the champion
            var updatedChamp = new ChampionInstance
            {
                instance_id = champ.instance_id,
                champ_id = champ.champ_id,
                player_id = champ.player_id,
                target_walk_pos = position,
                position = champ.position
                
            };

            // Delete and reinsert
            ctx.Db.champion_instance.instance_id.Delete(champ.instance_id);
            ctx.Db.champion_instance.Insert(updatedChamp);
        }
    }

    [Reducer]
    public static void AddChampion(ReducerContext ctx, string id, int base_ad, string name)
    {
        ctx.Db.champion_stats.Insert(new ChampionStats
        {
            champ_id = id,
            base_ad = base_ad,
            name = name

        });
    }

    [Reducer(ReducerKind.ClientConnected)]
    public static void Connect(ReducerContext ctx)
    {



        // Try to find existing player in logged_out table
        var loggedOutPlayer = ctx.Db.logged_out_player.identity.Find(ctx.Sender);
        Player player;

        if (loggedOutPlayer != null)
        {
            // Move from logged_out to active players
            player = loggedOutPlayer.Value;
            ctx.Db.player.Insert(player);
            ctx.Db.logged_out_player.identity.Delete(player.identity);
        }
        else
        {
            // Create new player - DO NOT set player_id manually
            player = new Player
            {
                identity = ctx.Sender,
                name = "",
                // player_id is NOT set here - it will be auto-assigned
            };
            ctx.Db.player.Insert(player);

            // Need to re-query to get the auto-assigned ID
            player = ctx.Db.player.identity.Find(ctx.Sender).Value;
        }

        // Now we can safely use player.player_id
        ctx.Db.champion_instance.Insert(new ChampionInstance
        {
            champ_id = "fiora",
            player_id = player.player_id,
            target_walk_pos = new DbVector2(0, 0)
        });
    }


    [Reducer(ReducerKind.ClientDisconnected)]
    public static void Disconnect(ReducerContext ctx)
    {

        Log.Info($"tick {tickCount} in {deltaTime.Microseconds}");


        var player = ctx.Db.player.identity.Find(ctx.Sender) ?? throw new Exception("Player not found");
       
        ctx.Db.logged_out_player.Insert(player);
        ctx.Db.player.identity.Delete(player.identity);

        foreach (var champ in ctx.Db.champion_instance.player_id.Filter(player.player_id))
        {
            ctx.Db.champion_instance.instance_id.Delete(champ.instance_id);
        }
    }

    [Reducer(ReducerKind.Init)]
    public static void Init(ReducerContext ctx)
    {

        startLogging = true;
        lastTimestamp = ctx.Timestamp;

        Log.Info("Adding default config");
        var config = new Config() { tick_length = .033f};
        
        ctx.Db.config.Insert(config);

        Log.Info("Adding Fiora as default champion");
        ctx.Db.champion_stats.Insert(new ChampionStats
        {
            champ_id = "fiora",
            base_ad = 50,
            name = "Fiora"

        });

        ScheduleMoveAllPlayers(ctx, 33_000);
       
    }
}

// This allows us to store 2D points in tables.
[SpacetimeDB.Type]
public partial struct DbVector2
{
    public float x;
    public float y;

    public DbVector2(float x, float y)
    {
        this.x = x;
        this.y = y;
    }

    public DbVector2 Normalized()
    {
        float magnitude = (float)Math.Sqrt(x * x + y * y);

        // Handle division by zero for zero-length vectors
        if (magnitude > 0)
        {
            return new DbVector2(x / magnitude, y / magnitude);
        }

        

        // Return zero vector if original vector has zero length
        return new DbVector2(0, 0);
    }

    // Returns the magnitude (length) of the vector
    public float Magnitude()
    {
        return (float)Math.Sqrt(x * x + y * y);
    }

    // Adds two vectors
    public static DbVector2 operator +(DbVector2 a, DbVector2 b)
    {
        return new DbVector2(a.x + b.x, a.y + b.y);
    }

    // Subtracts one vector from another
    public static DbVector2 operator -(DbVector2 a, DbVector2 b)
    {
        return new DbVector2(a.x - b.x, a.y - b.y);
    }
}