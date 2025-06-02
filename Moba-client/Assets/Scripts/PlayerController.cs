using SpacetimeDB.Types;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SocialPlatforms;

public class PlayerController : MonoBehaviour
{
    [SerializeField] private uint PlayerId;
    public static PlayerController Local { get; private set; }

    public LayerMask layerMask;

    public Material highlightMat;

    public Team team;

    private ActorController _currentHighlight;
    public ActorController CurrentHighlight
    {
        get
        {
            return _currentHighlight;
        }
        set
        {
            if (_currentHighlight == value) return;
            _currentHighlight?.RemoveOutline();
            _currentHighlight = value;
            if (value != null)
            {
                value.SetOutline(highlightMat);
            }
        }
    }

    public void Initialize(Player player)
    {
        team = player.Team;
        PlayerId = player.PlayerId;
        if (player.Identity == GameManager.LocalIdentity)
        {
            Local = this;
        }
    }

    private void Update()
    {
        if (Local != this) return;

        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        List<RaycastHit> hits = Physics.RaycastAll(ray, Mathf.Infinity, layerMask).ToList();
        hits.Sort((a, b) => a.distance.CompareTo(b.distance));
        for(int i = 0; i < hits.Count; i++)
        {
            hit = hits[i];

            bool consumedRay = false;

            Vector3 worldPosition = hit.point;
            ActorController newCurrentHighlight = null;

            ActorController actor = null;
            bool isPlayerChamp = false;

            if (hit.collider.CompareTag("Actor"))
            {
                if (hit.collider.TryGetComponent<ActorController>(out var hitActor))
                {
                    if (!hit.collider.TryGetComponent<ChampionController>(out var hitChamp) || hitChamp.owner != GameManager.LocalPlayerId)
                    {
                        newCurrentHighlight = hitActor;
                        consumedRay = true;
                    }
                    else
                    {
                        isPlayerChamp = true;
                    }
                }
            }

            CurrentHighlight = newCurrentHighlight;

            if (Input.GetMouseButtonDown(1))
            {
                switch (hit.collider.tag)
                {
                    case "Unit":
                        if (!isPlayerChamp)
                        {
                            if (actor.team != team)
                            {
                                // GameManager.Conn.Reducers.SetAttack();

                                consumedRay = true;
                            }
                        }
                        break;

                    case "Ground":
                        // Convert to DbVector2 (ignoring Y if you're using 2D coordinates)
                        DbVector2 targetPos = new DbVector2(worldPosition.x, worldPosition.z);

                        // Send to server
                        GameManager.Conn.Reducers.SetTargetWalkPos(targetPos);
                        consumedRay = true;
                        break;

                    default:
                        break;
                }

            }

            if (consumedRay) break;
        }
    }
}
