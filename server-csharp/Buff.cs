using SpacetimeDB;

public static partial class Module
{
    [Table(Name = "buff", Public = true)]
    [SpacetimeDB.Index.BTree(Name = "entity_id_and_buff_type", Columns = new[] { nameof(entity_id), nameof(buff_type) })]
    public partial struct Buff
    {
        [Unique, AutoInc, PrimaryKey]
        public uint buff_instance_id;

        [SpacetimeDB.Index.BTree]
        public uint entity_id;

        public string buff_id;

        public string buff_name;
        public string buff_description;
        public string source;

        public string buff_type;
        public float duration;
        public Timestamp start_timestamp;
        public float value;
        public int stacks;
    }


    [Reducer]
    public static void DoBuff(ReducerContext ctx, Buff buff)
    {
        if (GetTimestampDifferenceInSeconds(ctx.Timestamp, buff.start_timestamp) >= buff.duration)
        {
            ctx.Db.buff.buff_instance_id.Delete(buff.buff_instance_id);
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

   

    public static void AddBuff(ReducerContext ctx, Buff buff)
    {

    }

    public static void DeleteBuff()
    {

    }
}
