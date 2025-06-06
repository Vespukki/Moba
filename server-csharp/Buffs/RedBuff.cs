using SpacetimeDB;

public static partial class Module
{
    public static float RedBuffRegenCalculation(ReducerContext ctx, Buff buff)
    {
        Log.Info("red buff regen calc");
        var nullableActor = ctx.Db.actor.entity_id.Find(buff.entity_id);
        if (nullableActor == null)
        {
            ctx.Db.buff.buff_instance_id.Delete(buff.buff_instance_id);
            return 0;
        }
        Actor actor = nullableActor.Value;

        return 400; //TEMP
        return actor.max_health * .03f;

    }

    [Reducer]
    public static void CreateRedBuffComponents(ReducerContext ctx, Buff redBuff)
    {
        Buff healingComponent = new(redBuff.entity_id, BuffId.RedBuffRegen, redBuff.start_timestamp, redBuff.duration);
        Buff newHealingBuff = ctx.Db.buff.Insert(healingComponent);
        UpdateBuffValue(ctx, newHealingBuff);


        Buff burnOnHit = new(redBuff.entity_id, BuffId.RedBuffOnHit, redBuff.start_timestamp, redBuff.duration);
        Buff newBurnOnHit = ctx.Db.buff.Insert(burnOnHit);
        UpdateBuffValue(ctx, newBurnOnHit);
    }



}
