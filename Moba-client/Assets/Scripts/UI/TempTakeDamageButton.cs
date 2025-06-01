using UnityEngine;

public class TempTakeDamageButton : MonoBehaviour
{
    public void TakeDamage()
    {
        GameManager.Conn.Reducers.SetEntityHealth(1, 3000, 500);
    }
}
