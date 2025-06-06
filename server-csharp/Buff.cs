using SpacetimeDB;
using System.Diagnostics;

public static partial class Module
{
    [Type]
    public enum BuffId {RedBuff, RedBuffRegen, RedBuffOnHit, Burning};

    [Table(Name = "buff", Public = true)]
    [SpacetimeDB.Index.BTree(Name = "entity_id_and_buff_type", Columns = new[] { nameof(entity_id), nameof(buff_type) })]
    public partial struct Buff(uint entity_id, BuffId buff_id, Timestamp start_timestamp, float duration, 
        string buff_type = "", float value = 0, int stacks = 0, string source = "", uint buff_instance_id = 0)
    {

        [SpacetimeDB.Index.BTree]
        public uint entity_id = entity_id;

        public BuffId buff_id = buff_id; //E.G. red_buff
        public Timestamp start_timestamp = start_timestamp;
        public float duration = duration;


        public string buff_type = buff_type;
        
        public float value = value;
        public int stacks = stacks;

        public string source = source;

        [Unique, AutoInc, PrimaryKey]
        public uint buff_instance_id = buff_instance_id;
    }

    /*[Table(Name = "buff_on_hit", Public = true)]
    public partial struct BuffOnHit
    {
        [SpacetimeDB.Index.BTree]
        BuffId buff_id;

    }*/

    [Reducer]
    public static void DoBuff(ReducerContext ctx, Buff buff)
    {
        if (GetTimestampDifferenceInSeconds(ctx.Timestamp, buff.start_timestamp) >= buff.duration)
        {
            DeleteBuff(ctx, buff);
        }
        else
        {
            UpdateBuffValue(ctx, buff);
        }
    }

    [Reducer]
    public static void DoAllBuffs(ReducerContext ctx)
    {
        foreach (var buff in ctx.Db.buff.Iter())
        {
            DoBuff(ctx, buff);
        }
    }

    [Reducer]
    public static void AddBuff(ReducerContext ctx, Buff buff)
    {
        switch (buff.buff_id)
        {
            case BuffId.RedBuff:
                CreateRedBuffComponents(ctx, buff);
                break;

            default:
                break;
        }

        ctx.Db.buff.Insert(buff);
    }

    public static void DoBurn(ReducerContext ctx, Buff buff)
    {
        var nullableActor = ctx.Db.actor.entity_id.Find(buff.entity_id);

        if (nullableActor == null) return;

        Actor actor = nullableActor.Value;

        Log.Info("BURNING");
        SetActorHealth(ctx, actor, actor.max_health, actor.current_health - 200f * ((float)deltaTime.Microseconds / 1_000_000f));
    }

    [Reducer]
    public static void UpdateBuffValue(ReducerContext ctx, Buff buff)
    {
        Buff newBuff = buff;

        switch (buff.buff_id)
        {
            case BuffId.RedBuffRegen:
                newBuff.value = RedBuffRegenCalculation(ctx, buff);
                break;
            case BuffId.Burning:
                DoBurn(ctx, buff);
                break;
            default:
                return; //dont update the buff if nothing needed updating
        }

        if (newBuff != buff)
        {
            ctx.Db.buff.buff_instance_id.Delete(buff.buff_instance_id);
            ctx.Db.buff.Insert(newBuff);
        }
    }

 
   

    [Reducer]
    public static void DeleteBuff(ReducerContext ctx, Buff buff)
    {
        ctx.Db.buff.buff_instance_id.Delete(buff.buff_instance_id);
    }
}
