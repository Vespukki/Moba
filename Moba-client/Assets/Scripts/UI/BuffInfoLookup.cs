using System.Collections.Generic;
using UnityEngine;
using SpacetimeDB;
using SpacetimeDB.Types;

public static class BuffInfoLookup
{
    private static Dictionary<BuffId, BuffDisplayInfo> buffIdToDisplayInfo = new()
    {

        {BuffId.RedBuff, new(true, false, "Crest of Cinders",
        "This unit recovers health when not fighting champions or epic monsters. Also, their basic\n" +
        "attacks burn and slow the target over several seconds.")},

        {BuffId.Burning, new(true, false, "Burning", "This unit is taking damage over time.")},
        {BuffId.Slowed, new(true, false, "Slowed", "This unit's Move Speed is slowed.")}


    };

    public static BuffDisplayInfo GetInfo(BuffId buffId)
    {
        if (buffIdToDisplayInfo.TryGetValue(buffId, out BuffDisplayInfo displayInfo))
        {
            return displayInfo;
        }
        else
        {
            return new(false,false, "","shouldn't be seeing this!!!");
        }
    }
}

public struct BuffDisplayInfo
{
    public bool visible;
    public bool showZeroStacks;
    public string buffName;
    public string buffDescription;

    public BuffDisplayInfo(bool visible, bool showZeroStacks, string buffName, string buffDescription)
    {
        this.visible = visible;
        this.showZeroStacks = showZeroStacks;
        this.buffName = buffName;
        this.buffDescription = buffDescription;
    }
}