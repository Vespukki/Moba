using SpacetimeDB.Types;
using UnityEngine;
using UnityEngine.SocialPlatforms;

public class PlayerController : MonoBehaviour
{
    [SerializeField] private uint PlayerId;
    public static PlayerController Local { get; private set; }


    public void Initialize(Player player)
    {
        PlayerId = player.PlayerId;
        if (player.Identity == GameManager.LocalIdentity)
        {
            Local = this;
        }
    }

    private void Update()
    {
        if (Local != this) return;
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit, Mathf.Infinity))
            {
                Vector3 worldPosition = hit.point;

                // Convert to DbVector2 (ignoring Y if you're using 2D coordinates)
                DbVector2 targetPos = new DbVector2(worldPosition.x, worldPosition.z);

                // Send to server
                GameManager.Conn.Reducers.SetTargetWalkPos(targetPos);
            }
        }
    }
}
