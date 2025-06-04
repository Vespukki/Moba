using SpacetimeDB;
using SpacetimeDB.Internal.TableHandles;

public static partial class Module
{
    [Table(Name = "champion_stats", Public = true)]
    public partial struct ChampionStats
    {
        [PrimaryKey, Unique]
        public string champ_id;
        public string name;
        public int base_ad;
        public uint attack_range;
    }

    

    [Table(Name = "champion_instance", Public = true)]
    public partial struct ChampionInstance
    {
        [PrimaryKey, Unique]
        public uint entity_id;

        public string champ_id;

        [SpacetimeDB.Index.BTree]
        public uint player_id;


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

    [Reducer]
    public static void CreateChampionInstance(ReducerContext ctx, ChampionInstance champ)
    {
        var _champStats = ctx.Db.champion_stats.champ_id.Find(champ.champ_id);
        ChampionStats champStats;
        if (_champStats != null) champStats = _champStats.Value;
        else return;
        var newEntity = ctx.Db.entity.Insert(new Entity() 
        {
            entity_id = 0, //auto increments
            position = new(0,0),
            last_position = new(0,0),
        });

        var player = ctx.Db.player.player_id.Find(champ.player_id);

        Team teamToBe = Team.Neutral;

        if (player != null)
        {
            teamToBe = player.Value.team;
        }

        var newActor = ctx.Db.actor.Insert(new Actor()
        {
            entity_id = newEntity.entity_id,
            max_health = 2000f,
            current_health = 1000f,
            rotation = 0,
            team = teamToBe,
            attack_range = champStats.attack_range,
            attack_speed = .69f,
            windup_percent = .13793f,
            last_attack_time = ctx.Timestamp,
            health_regen = 0f
        });

        ChampionInstance newChamp = new()
        {
            player_id = champ.player_id,
            champ_id = champ.champ_id,
            entity_id = newEntity.entity_id
        };
        Log.Info($"Entity id of new champ is {newEntity.entity_id}");

        ctx.Db.buff.Insert(new Buff()
        {
            start_timestamp = ctx.Timestamp,
            duration = 5f,
            entity_id = newEntity.entity_id,    
            buff_type = "health_regen",
            stacks = 0,
            value = 200f
        });
        
        ctx.Db.champion_instance.Insert(newChamp);
    }

    [Reducer]
    public static void DeleteChampionInstance(ReducerContext ctx, ChampionInstance champ)
    {
        uint entityID = champ.entity_id;
        ctx.Db.champion_instance.entity_id.Delete(entityID);
        ctx.Db.actor.entity_id.Delete(entityID);
        ctx.Db.entity.entity_id.Delete(entityID);

        foreach (var buff in ctx.Db.buff.entity_id.Filter(champ.entity_id))
        {
            ctx.Db.buff.buff_instance_id.Delete(buff.buff_instance_id);
        }
    }

}