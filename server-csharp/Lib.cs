using SpacetimeDB;
using System.Xml.Linq;

public static partial class Module
{
    public static Timestamp lastTimestamp;
    public static TimeDuration deltaTime;

    public const int TICK_LENGTH_MICROSECONDS = 30_000;

    [Type]
    public enum Team { Red, Blue, Neutral }

    [Table(Name = "player", Public = true)]
    [Table(Name = "logged_out_player", Public = true)]
    public partial struct Player
    {
        [PrimaryKey]
        public Identity identity;

        [Unique, AutoInc]
        public uint player_id;
        public string name;
        public Team team;
    }

    [Table(Name = "tick_timer", Scheduled = nameof(DoTick), ScheduledAt = nameof(scheduled_at))]
    public partial struct TickTimer
    {
        [PrimaryKey, AutoInc]
        public ulong scheduled_id;
        public ScheduleAt scheduled_at;
    }

    [Reducer]
    public static void DoTick(ReducerContext ctx, TickTimer timer)
    {
        deltaTime = ctx.Timestamp.TimeDurationSince(lastTimestamp);
        lastTimestamp = ctx.Timestamp;

        MoveAllPlayers(ctx);
        MakeAllAttacks(ctx);
        DoAllBuffs(ctx);
        DoAllActorUpkeep(ctx);
    }

    [Reducer(ReducerKind.ClientConnected)]
    public static void Connect(ReducerContext ctx)
    {
        var loggedOutPlayer = ctx.Db.logged_out_player.identity.Find(ctx.Sender);
        Player player;

        if (loggedOutPlayer != null)
        {
            player = loggedOutPlayer.Value;
            ctx.Db.player.Insert(player);
            ctx.Db.logged_out_player.identity.Delete(player.identity);
        }
        else
        {
            player = new Player
            {
                identity = ctx.Sender,
                name = "",
                team = Team.Blue
            };
            player = ctx.Db.player.Insert(player);

        }
        var champ = new ChampionInstance
        {
            champ_id = ChampId.Fiora,
            player_identity = player.identity,
        };
        CreateChampionInstance(ctx, champ, ActorId.Fiora);

        Log.Info($"player {player.player_id} Connected");


    }


    [Reducer(ReducerKind.ClientDisconnected)]
    public static void Disconnect(ReducerContext ctx)
    {

        var player = ctx.Db.player.identity.Find(ctx.Sender) ?? throw new Exception("Player not found");
        Log.Info($"player {player.player_id} disconnected");

        ctx.Db.logged_out_player.Insert(player);
        ctx.Db.player.identity.Delete(player.identity);

        foreach (var champ in ctx.Db.champion_instance.player_identity.Filter(player.identity))
        {
            DeleteChampionInstance(ctx, champ);
        }
    }

    [Reducer(ReducerKind.Init)]
    public static void Init(ReducerContext ctx)
    {

        lastTimestamp = ctx.Timestamp;

        Log.Info("Adding default config");

        Log.Info("Adding Fiora as default champion");
        ctx.Db.champion_stats.Insert(new ChampionStats
        {
            champ_id = (uint)ChampId.Fiora,
            basic_attack_ability_id = AbilityId.BasicAttack,
            q_ability_id = AbilityId.FioraQ

        });

        Log.Info("Adding Target Dummy as default champion");
        ctx.Db.champion_stats.Insert(new ChampionStats
        {
            champ_id = (uint)ChampId.Dummy,
            basic_attack_ability_id = (uint)AbilityId.None,
            q_ability_id = (uint)AbilityId.None,
            
        });

        ctx.Db.actor_base_stats.Insert(new ActorBaseStats()
        {
            actor_id = (uint)ActorId.Fiora,
            max_health = 2000f,
            attack_range = 150f,
            attack_speed = .69f,
            windup_percent = .13793f,
            health_regen = 0f,
            armor = 33f,
            attack = 66f,
            magic_resist = 32,
            move_speed = 345
        });

        ctx.Db.actor_base_stats.Insert(new ActorBaseStats()
        {
            actor_id = (uint)ActorId.Dummy,
            max_health = 3000f,
            attack_range = 0f,
            attack_speed = 0f,
            windup_percent = 1f,
            health_regen = 30f,
            armor = 0f,
            attack = 0f,
            magic_resist = 0f,
            move_speed = 0f
        });


        //init buff relations
        ctx.Db.buff_health_regen.Insert(new(BuffId.RedBuffRegen));
        ctx.Db.buff_on_hit.Insert(new(BuffId.RedBuffOnHit));

        var tickTimer = new TickTimer
        {
            scheduled_at = new ScheduleAt.Interval(new TimeDuration(33_000))
        };

        ctx.Db.tick_timer.Insert(tickTimer);

    }
}

