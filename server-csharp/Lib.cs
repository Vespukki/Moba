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
            champ_id = "fiora",
            player_id = player.player_id,
        };
        CreateChampionInstance(ctx, champ);

        Log.Info($"player {player.player_id} Connected");


    }


    [Reducer(ReducerKind.ClientDisconnected)]
    public static void Disconnect(ReducerContext ctx)
    {

        var player = ctx.Db.player.identity.Find(ctx.Sender) ?? throw new Exception("Player not found");
        Log.Info($"player {player.player_id} disconnected");

        ctx.Db.logged_out_player.Insert(player);
        ctx.Db.player.identity.Delete(player.identity);

        foreach (var champ in ctx.Db.champion_instance.player_id.Filter(player.player_id))
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
            champ_id = "fiora",
            base_ad = 50,
            name = "Fiora",
            attack_range = 150,
            
        });

        Log.Info("Adding Target Dummy as default champion");
        ctx.Db.champion_stats.Insert(new ChampionStats
        {
            champ_id = "dummy",
            base_ad = 0,
            name = "Target Dummy",
            attack_range = 0,
            
        });

        var tickTimer = new TickTimer
        {
            scheduled_at = new ScheduleAt.Interval(new TimeDuration(33_000))
        };

        ctx.Db.tick_timer.Insert(tickTimer);

    }
}

