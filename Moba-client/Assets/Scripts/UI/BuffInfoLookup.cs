using System.Collections.Generic;
using UnityEngine;

public static class BuffInfoLookup
{
    private static Dictionary<string, BuffDisplayInfo> buffIdToDisplayInfo = new()
    {
        {"red_buff_regen", new(false, false)}
    };

    public static BuffDisplayInfo GetInfo(string buffId)
    {
        if (buffIdToDisplayInfo.TryGetValue(buffId, out BuffDisplayInfo displayInfo))
        {
            return displayInfo;
        }
        else
        {
            return new(true, false);
        }
    }
}

public struct BuffDisplayInfo
{
    public bool visible;
    public bool showZeroStacks;

    public BuffDisplayInfo(bool visible = true, bool showZeroStacks = false)
    {
        this.visible = visible;
        this.showZeroStacks = showZeroStacks;
    }
}