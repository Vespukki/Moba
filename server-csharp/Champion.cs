using SpacetimeDB;

[Type]
public enum ChampId { Dummy, Fiora }
public static partial class Module
{
    [Table(Name = "champion_stats", Public = true)]
    public partial struct ChampionStats
    {
        [PrimaryKey, Unique]
        public uint champ_id;

        public AbilityId basic_attack_ability_id;
        public AbilityId q_ability_id;
    }

    

    [Table(Name = "champion_instance", Public = true)]
    public partial struct ChampionInstance
    {
        [PrimaryKey, Unique]
        public uint entity_id;

        public ChampId champ_id;

        [SpacetimeDB.Index.BTree]
        public Identity player_identity;

        public uint basic_attack_ability_instance_id;
        public uint q_ability_instance_id;
    }

  

    [Reducer]
    public static void AddChampion(ReducerContext ctx, ChampionStats champStats)
    {
        ctx.Db.champion_stats.Insert(champStats);
    }

    [Reducer]
    public static void CreateChampionInstance(ReducerContext ctx, ChampionInstance champ, ActorId actorId)
    {
        var nChampStats = ctx.Db.champion_stats.champ_id.Find((uint)champ.champ_id);
        if (nChampStats == null) return;
        ChampionStats champStats = nChampStats.Value;

        Ability newBasicAttack = ctx.Db.ability.Insert(new()
        {
            ability_instance_id = 0,
            ability_id = champStats.basic_attack_ability_id,
            ready_time = ctx.Timestamp,
            targeted = true
        });

        Ability newQAbility = ctx.Db.ability.Insert(new()
        {
            ability_id = champStats.q_ability_id,
            ability_instance_id = 0,
            ready_time = ctx.Timestamp,
            targeted = false
        });

        var newEntity = ctx.Db.entity.Insert(new Entity() 
        {
            entity_id = 0, //auto increments
            position = new(0,0),
            last_position = new(0,0),
        });

        var player = ctx.Db.player.identity.Find(champ.player_identity);

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
            player_identity = champ.player_identity,
            champ_id = champ.champ_id,
            entity_id = newEntity.entity_id,
            basic_attack_ability_instance_id = newBasicAttack.ability_instance_id,
            q_ability_instance_id = newQAbility.ability_instance_id,
            
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

        ctx.Db.ability.ability_instance_id.Delete(champ.basic_attack_ability_instance_id);
        ctx.Db.ability.ability_instance_id.Delete(champ.q_ability_instance_id);
    }

}