using SpacetimeDB.Types;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SocialPlatforms;

public class PlayerController : MonoBehaviour
{
    [SerializeField] private uint PlayerId;
    public static PlayerController Local { get; private set; }

    public LayerMask layerMask;

    public Transform mouseTransform;

    public Team team;

    private IHoverable _currentHover;

    private ActorController _currentSelected;

    public ActorController CurrentSelected
    {
        get
        {
            return _currentSelected;
        }
        set
        {
            if (_currentSelected != null)
            {
                _currentSelected.EndSelect();
            }
            _currentSelected = value;
            if (value == null)
            {
                SelectionMenu.instance.gameObject.SetActive(false);
            }
            else
            {
                SelectionMenu.instance.gameObject.SetActive(true);
                SelectionMenu.instance.Initialize(value);
                value.BeginSelect();
            }
        }
    }

    public List<uint> ownedEntities;
    public IHoverable CurrentHover
    {
        get
        {
            return _currentHover;
        }
        set
        {
            if (_currentHover == value) return;

            _currentHover?.EndHover();
            _currentHover = value;
            if (value != null)
            {
                value.BeginHover();
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

    private void HandleMousePos()
    {
        PointerEventData pointerData = new PointerEventData(EventSystem.current)
        {
            position = Input.mousePosition
        };

        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointerData, results);


        List<RaycastResult> uiHits = new();

        foreach (var result in results)
        {
            if (result.gameObject.CompareTag("UI"))
            {
                uiHits.Add(result);
            }
        }

        
        if (uiHits.Count > 0)
        {
            uiHits.Sort((a, b) => a.distance.CompareTo(b.distance));

            for (int i = 0; i < uiHits.Count(); i++)
            {
                var uiHit = uiHits[i];

                if (uiHit.gameObject.TryGetComponent(out IHoverable hover))
                {
                    CurrentHover = hover;
                    return;
                }
            }

            CurrentHover = null;
            return;
        }


        //past here we assert that there are no interactable UI elements under the mouse

        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        List<RaycastHit> hits = Physics.RaycastAll(ray, Mathf.Infinity, layerMask).ToList();
        hits.Sort((a, b) => a.distance.CompareTo(b.distance));

        for (int i = 0; i < hits.Count; i++)
        {
            RaycastHit newHit = hits[i];
        }

        for (int i = 0; i < hits.Count; i++)
        {
            hit = hits[i];

            bool consumedRay = false;

            Vector3 worldPosition = hit.point;
            ActorController newCurrentHighlight = null;

            ChampionController champ = null;
            bool isPlayerChamp = false;

            if (hit.collider.CompareTag("Actor"))
            {
                if (hit.collider.TryGetComponent<ActorController>(out var hitActor))
                {
                    if (!hit.collider.TryGetComponent<ChampionController>(out var hitChamp) || hitChamp.ownerPlayerId != GameManager.LocalPlayerId)
                    {
                        champ = hitChamp;
                        newCurrentHighlight = hitActor;
                        consumedRay = true;
                    }
                    else
                    {
                        isPlayerChamp = true;
                    }
                }
            }

            CurrentHover = newCurrentHighlight;

            if (Input.GetMouseButtonDown(0))
            {
                switch (hit.collider.tag)
                {
                    case "Actor":
                        if (!isPlayerChamp)
                        {
                            CurrentSelected = champ;

                            consumedRay = true;
                        }
                        break;

                    case "Ground":
                        CurrentSelected = champ;
                        consumedRay = true;
                        break;
                    case "UI":
                        break;
                    default:
                        break;
                }
            }
            else if (Input.GetMouseButtonDown(1))
            {
                switch (hit.collider.tag)
                {
                    case "Actor":
                        if (!isPlayerChamp)
                        {
                            if (champ != null && champ.team != team)
                            {
                                Debug.Log($"going to attack {champ.gameObject.name} now");

                                foreach (var id in ownedEntities)
                                {
                                    GameManager.Conn.Reducers.SetAttackTarget(id, champ.entityId);
                                }

                                consumedRay = true;
                            }
                        }
                        break;

                    case "Ground":
                        // Convert to DbVector2 (ignoring Y if you're using 2D coordinates)
                        DbVector2 targetPos = new DbVector2(worldPosition.x, worldPosition.z);

                        // Send to server
                        foreach (var id in ownedEntities)
                        {
                            GameManager.Conn.Reducers.SetTargetWalkPos(id, targetPos, true);
                        }
                        consumedRay = true;
                        break;

                    case "UI":
                        consumedRay = true;
                        break;
                    default:
                        break;
                }

            }
            else if (Input.GetKeyDown(KeyCode.Q))
            {
                switch (hit.collider.tag)
                {
                    case "Actor":
                        foreach (var id in ownedEntities)
                        {
                            Debug.Log(champ.championInstance.QAbilityInstanceId + "q press");
                            GameManager.Conn.Reducers.SetQTarget(id, champ.entityId, EntityController.WorldPositionToDbPosition(worldPosition));
                        }
                        consumedRay = true;
                        break;
                    case "Ground":
                        foreach (var id in ownedEntities)
                        {
                            GameManager.Conn.Reducers.SetQTarget(id, id, EntityController.WorldPositionToDbPosition(worldPosition));
                        }
                        consumedRay = true;
                        break;
                    default:
                        break;
                }

            }

            if (consumedRay) return;
        }
    }


    private void Update()
    {
        if (Local != this) return;

        mouseTransform.position = Input.mousePosition;
        HandleMousePos();
    }
}
