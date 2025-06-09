using SpacetimeDB;
using SpacetimeDB.Internal.TableHandles;

public static partial class Module
{
    [Table(Name = "champion_stats", Public = true)]
    public partial struct ChampionStats
    {
        [PrimaryKey, Unique]
        public string champ_id;
    }

    

    [Table(Name = "champion_instance", Public = true)]
    public partial struct ChampionInstance
    {
        [PrimaryKey, Unique]
        public uint entity_id;

        public string champ_id;

        [SpacetimeDB.Index.BTree]
        public uint player_id;

        public uint basic_attack_ability_instance_id;
    }

  

    [Reducer]
    public static void AddChampion(ReducerContext ctx, string id, int base_ad, string name)
    {
        ctx.Db.champion_stats.Insert(new ChampionStats
        {
            champ_id = id,
        });
    }

    [Reducer]
    public static void CreateChampionInstance(ReducerContext ctx, ChampionInstance champ, ActorId actorId)
    {
        Ability newBasicAttack = ctx.Db.ability.Insert(new()
        {
            ability_instance_id = 0,
            ability_id = (uint)AbilityId.BasicAttack,
            ready_time = ctx.Timestamp,
        });

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
            name = "Fiora",
            current_health = 1000f,
            rotation = 0,
            team = teamToBe,
            last_attack_time = ctx.Timestamp,
            actor_id = (uint)actorId
        });

        ChampionInstance newChamp = new()
        {
            player_id = champ.player_id,
            champ_id = champ.champ_id,
            entity_id = newEntity.entity_id,
            basic_attack_ability_instance_id = newBasicAttack.ability_instance_id
            
        };
        Log.Info($"Entity id of new champ is {newEntity.entity_id}");

       
        ctx.Db.champion_instance.Insert(newChamp);

        /*Buff redBuff = new(newEntity.entity_id, BuffId.RedBuff, ctx.Timestamp, 20f, source: newActor.name);
        AddBuff(ctx, redBuff);*/

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