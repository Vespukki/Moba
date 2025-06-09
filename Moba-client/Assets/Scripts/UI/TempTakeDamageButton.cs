using UnityEngine;

public class TempTakeDamageButton : MonoBehaviour
{
    public void TakeDamage()
    {
        GameManager.Conn.Reducers.CreateChampionInstance(new SpacetimeDB.Types.ChampionInstance()
        {
            ChampId = "dummy",
            EntityId = 0,
            PlayerId = 696969
        },
        SpacetimeDB.Types.ActorId.Dummy
        );
    }
}
