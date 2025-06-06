using SpacetimeDB.Types;
using System.Collections.Generic;
using UnityEngine;

//should be 2 in the scene i think, one for self and one for target display
public class BuffManager : MonoBehaviour
{
    ActorController target;
    public Transform buffHolder;

    public Dictionary<Buff, GameObject> buffObjects = new();

    public void Initialize(ActorController actor)
    {
        target = actor;
        ActorController.OnBuffAdded += AddBuff;
        ActorController.OnBuffRemoved += RemoveBuff;

        foreach (var key in buffObjects.Keys)
        {
            RemoveBuff(actor, key);
        }

        foreach (var buff in actor.GetBuffs())
        {
            AddBuff(actor, buff);
        }
    }

    private void OnDestroy()
    {
        ActorController.OnBuffAdded -= AddBuff;
        ActorController.OnBuffRemoved -= RemoveBuff;
    }

    public void AddBuff(ActorController actor, Buff buff)
    {
        if (actor != target) return;

        BuffDisplayInfo displayInfo = BuffInfoLookup.GetInfo(buff.BuffId);

        if (displayInfo.visible)
        {
            var spawned = PrefabManager.SpawnBuffDisplay(buff, displayInfo ,buffHolder);
            buffObjects.Add(buff, spawned);
        }
    }

    public void RemoveBuff(ActorController actor, Buff buff)
    {
        if (actor != target) return;

        if (BuffInfoLookup.GetInfo(buff.BuffId).visible)
        {
                GameObject buffObject = buffObjects[buff];
            Destroy(buffObject);
            buffObjects.Remove(buff);
        }
    }
}
