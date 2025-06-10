using UnityEngine;
using SpacetimeDB.Types;

public class TempTakeDamageButton : MonoBehaviour
{
    public void TakeDamage()
    {
        GameManager.Conn.Reducers.CreateChampionInstance(new SpacetimeDB.Types.ChampionInstance()
        {
            ChampId = 0,
            EntityId = 0,
            PlayerIdentity = new()
        },
        SpacetimeDB.Types.ActorId.Dummy
        );
    }
}
