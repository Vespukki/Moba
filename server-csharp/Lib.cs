using SpacetimeDB;
using System.Xml.Linq;

public static partial class Module
{
    public static Timestamp lastTimestamp;
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
        public Team team;
    }

    [Table(Name = "move_all_players_timer", Scheduled = nameof(MoveAllPlayers), ScheduledAt = nameof(scheduled_at))]
    public partial struct MoveAllPlayersTimer
    {
        [PrimaryKey, AutoInc]
        public ulong scheduled_id;
        public ScheduleAt scheduled_at;
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
                team = Team.Blue
            };
            ctx.Db.player.Insert(player);

            // Need to re-query to get the auto-assigned ID
            player = ctx.Db.player.identity.Find(ctx.Sender).Value;
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
            name = "Fiora"

        });

        Log.Info("Adding Target Dummy as default champion");
        ctx.Db.champion_stats.Insert(new ChampionStats
        {
            champ_id = "dummy",
            base_ad = 0,
            name = "Target Dummy"

        });

        ScheduleMoveAllPlayers(ctx, 33_000);
       
    }
}

