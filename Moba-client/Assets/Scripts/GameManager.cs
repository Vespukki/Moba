using SpacetimeDB;
using SpacetimeDB.Types;
using System;
using System.Collections.Generic;
using UnityEngine;
using static UnityEngine.EventSystems.EventTrigger;

public class GameManager : MonoBehaviour
{
    const string SERVER_URL = "http://127.0.0.1:3000";
    const string MODULE_NAME = "moba";

    public static event Action OnConnected;
    public static event Action OnSubscriptionApplied;

    public BuffManager localBuffManager;
    public static GameManager Instance { get; private set; }
    public static Identity LocalIdentity { get; private set; }
    public static uint LocalPlayerId { get; private set; }
    public static DbConnection Conn { get; private set; }

    public Dictionary<uint, ChampionController> championControllers = new();
    public Dictionary<uint, ChampionInstance> championInstances = new();
    public Dictionary<uint, BuffDisplay> buffIdToBuffDisplay = new();
    public Dictionary<uint, List<Buff>> buffsByEntityId = new(); //EntityID = 0 means its pending an assignment (or maybe its lost?)
    public Dictionary<uint, Ability> abilities = new();

    public AbilityDisplay qAbilityDisplay;

    public bool gameInitialized = false;

    private void Start()
    {
        Instance = this;
        Application.targetFrameRate = 60;

        // In order to build a connection to SpacetimeDB we need to register
        // our callbacks and specify a SpacetimeDB server URI and module name.
        DbConnectionBuilder<DbConnection> builder = DbConnection.Builder()
            .OnConnect(HandleConnect)
            .OnConnectError(HandleConnectError)
            .OnDisconnect(HandleDisconnect)
            .WithUri(SERVER_URL)
            .WithModuleName(MODULE_NAME);

        // If the user has a SpacetimeDB auth token stored in the Unity PlayerPrefs,
        // we can use it to authenticate the connection.
        if (AuthToken.Token != "")
        {
            builder = builder.WithToken(AuthToken.Token);
        }

        // Building the connection will establish a connection to the SpacetimeDB
        // server.
        Conn = builder.Build();
    }

    // Called when we connect to SpacetimeDB and receive our client identity
    void HandleConnect(DbConnection conn, Identity identity, string token)
    {
        Debug.Log("Connected.");
        AuthToken.SaveToken(token);
        LocalIdentity = identity;

        conn.Db.ChampionStats.OnInsert += ChampionStatsOnInsert;
        
        conn.Db.Player.OnInsert += PlayerOnInsert;
        conn.Db.Player.OnDelete += PlayerOnDelete;

        conn.Db.Buff.OnInsert += BuffOnInsert;
        conn.Db.Buff.OnDelete += BuffOnDelete;
        conn.Db.Buff.OnUpdate += BuffOnUpdate;

        conn.Db.ChampionInstance.OnInsert += ChampionInstanceOnInsert;
        conn.Db.ChampionInstance.OnUpdate += ChampionInstanceOnUpdate;
        
        conn.Db.Entity.OnUpdate += EntityOnUpdate;
        
        conn.Db.Actor.OnUpdate += ActorOnUpdate;
        conn.Db.Actor.OnInsert += ActorOnInsert;
        
        conn.Db.Walking.OnDelete += WalkingOnDelete;
        conn.Db.Walking.OnUpdate += WalkingOnUpdate;
        conn.Db.Walking.OnInsert += WalkingOnInsert;
        
        conn.Db.Attacking.OnInsert += AttackingOnInsert;
        conn.Db.Attacking.OnUpdate += AttackingOnUpdate;
        conn.Db.Attacking.OnDelete += AttackingOnDelete;

        conn.Db.RegisteredHits.OnInsert += RegisteredHitsOnInsert;

        conn.Db.Ability.OnInsert += AbilityOnInsert;
        conn.Db.Ability.OnUpdate += AbilityOnUpdate;



        OnConnected?.Invoke();

        // Request all tables
        Conn.SubscriptionBuilder()
            .OnApplied(HandleSubscriptionApplied)
            .SubscribeToAllTables();
    }

    private void AbilityOnUpdate(EventContext context, Ability oldRow, Ability newRow)
    {
        if (newRow.AbilityInstanceId == qAbilityDisplay.ability.AbilityInstanceId)
        {
            qAbilityDisplay.UpdateAbility(newRow);
        }
    }

    private void BuffOnUpdate(EventContext context, Buff oldRow, Buff newRow)
    {
        /*if (buffIdToBuffDisplay.TryGetValue(oldRow.BuffInstanceId, out BuffDisplay display))
        {
            display.UpdateBuff(newRow, context.Event.Time);
        }*/
    }

    private void AbilityOnInsert(EventContext ctx, Ability ability)
    {
        Debug.Log("Ability Added");
        abilities.Add(ability.AbilityInstanceId, ability);
    }

    private void BuffOnDelete(EventContext context, Buff buff)
    {
        Debug.Log("buff removed");
        if (championControllers.TryGetValue(buff.EntityId, out ChampionController champ))
        {
            champ.RemoveBuff(buff);

            if (buffsByEntityId.TryGetValue(champ.entityId, out List<Buff> buffs))
            {
                buffs.Remove(buff);
            }
            else
            {
                Debug.LogError("tried to remove buff from champion that didnt have a buff list, so something aint right");
            }
          
        }
    }

    private void BuffOnInsert(EventContext context, Buff buff)
    {
        Debug.Log("buff added");
        if (championControllers.TryGetValue(buff.EntityId, out ChampionController champ))
        {
            champ.AddBuff(buff);


            if (buffsByEntityId.TryGetValue(champ.entityId, out List<Buff> buffs))
            {
                buffs.Add(buff);
            }
            else
            {
                var newList = new List<Buff>() { buff };
                buffsByEntityId.Add(champ.entityId, newList);
            }
        }
        else
        {
            //assume the champion hasnt spawned yet I guess
            if (buffsByEntityId.TryGetValue(0, out List<Buff> buffs))
            {
                buffs.Add(buff);
            }
            else
            {
                var newList = new List<Buff>() { buff };
                buffsByEntityId.Add(0, newList);
            }
        }
    }

    private void RegisteredHitsOnInsert(EventContext context, RegisteredHits row)
    {
        if (championControllers.TryGetValue(row.HitEntityId, out ChampionController hitChamp)
            && championControllers.TryGetValue(row.SourceEntityId, out ChampionController sourceChamp))
        {
            hitChamp.PlayVFX(sourceChamp.hitVfx);
        }
    }

    private void AttackingOnDelete(EventContext context, Attacking row)
    {
        if (championControllers.TryGetValue(row.EntityId, out var champController))
        {
            champController.AttackingDeleted(row);
        }
    }

    private void AttackingOnUpdate(EventContext context, Attacking oldRow, Attacking newRow)
    {
        if (championControllers.TryGetValue(newRow.EntityId, out var champController))
        {
            champController.UpdateAttacker(oldRow, newRow);
        }
    }

    private void AttackingOnInsert(EventContext context, Attacking row)
    {
        if(championControllers.TryGetValue(row.EntityId, out var champController))
        {
           champController.AttackingCreated(row);
        }
    }

    private void PlayerOnDelete(EventContext context, Player row)
    {
        Debug.Log("Player deleted");
    }
    private void ActorOnInsert(EventContext context, Actor row)
    {
        if (championControllers.TryGetValue(row.EntityId, out ChampionController champController))
        {
            champController.InsertActor(row);
            Debug.Log("actor on Insert");
        }
        
    }

    private void ActorOnUpdate(EventContext context, Actor oldRow, Actor newRow)
    {
        if (championControllers.TryGetValue(oldRow.EntityId, out ChampionController champController))
        {
            champController.UpdateActor(oldRow, newRow);
        }

        
    }

    private void EntityOnUpdate(EventContext context, Entity oldRow, Entity newRow)
    {
        if (championControllers.TryGetValue(oldRow.EntityId, out ChampionController champController))
        {
            champController.UpdateEntity(newRow);
        }
    }

    private void HandleSubscriptionApplied(SubscriptionEventContext ctx)
    {
        Debug.Log("Subscription applied!");

        //spawn the champions
        foreach (var champ in championInstances.Values)
        {
            Entity entity = ctx.Db.Entity.EntityId.Find(champ.EntityId);
            Actor actor = ctx.Db.Actor.EntityId.Find(champ.EntityId);
            ActorBaseStats baseStats = ctx.Db.ActorBaseStats.ActorId.Find(actor.ActorId);

            SpawnChampion(entity, actor, champ, baseStats);
        }

        gameInitialized = true;
        OnSubscriptionApplied?.Invoke();
    }

    private void SpawnChampion(Entity entity, Actor actor, ChampionInstance champ, ActorBaseStats baseStats)
    {

        var spawned = PrefabManager.SpawnChampion(entity, actor, champ, baseStats);
        championControllers.Add(champ.EntityId, spawned);


        if (champ.PlayerIdentity == LocalIdentity)
        {
            PlayerController.Local.ownedEntities.Add(champ.EntityId);
            localBuffManager.Initialize(spawned);
        }
    }

    void HandleConnectError(Exception ex)
    {
        Debug.LogError($"Connection error: {ex}");
    }

    void HandleDisconnect(DbConnection _conn, Exception ex)
    {
        Debug.Log("Disconnected.");
        if (ex != null)
        {
            Debug.LogException(ex);
        }
    }

    private void ChampionInstanceOnInsert(EventContext ctx, ChampionInstance champ)
    {
        Debug.Log("Champion instance added");

        championInstances.Add(champ.EntityId, champ);

        if (gameInitialized)
        {
            Entity entity = ctx.Db.Entity.EntityId.Find(champ.EntityId);
            Actor actor = ctx.Db.Actor.EntityId.Find(champ.EntityId);
            ActorBaseStats baseStats = ctx.Db.ActorBaseStats.ActorId.Find(actor.ActorId);

            SpawnChampion(entity, actor, champ, baseStats);
        }
       

        List<Buff> buffsToMoveOver = new();
        if (buffsByEntityId.TryGetValue(0, out List<Buff> buffList))
        {
            foreach (var buff in buffList) //the unassigned ones
            {
                if (buff.EntityId == champ.EntityId)
                {
                    buffsToMoveOver.Add(buff);
                }
            }

            foreach (var buff in buffsToMoveOver)
            {

                buffsByEntityId.Remove(buff.BuffInstanceId);
                ChampionController champController = championControllers[champ.EntityId];
                champController.AddBuff(buff);
                if (buffsByEntityId.TryGetValue(champ.EntityId, out List<Buff> buffs))
                {
                    buffs.Add(buff);
                }
                else
                {
                    var newList = new List<Buff>() { buff };
                    buffsByEntityId.Add(champ.EntityId, newList);

                }
            }
        }
    }

    private void PlayerOnInsert(EventContext ctx, Player player)
    {
        if (player.Identity != LocalIdentity) return;
        LocalPlayerId = player.PlayerId;

        var playerController = PrefabManager.SpawnPlayer(player);


        foreach (var champ in championControllers.Values)
        {
            if (champ.ownerPlayerId == player.PlayerId)
            {
                playerController.ownedEntities.Add(champ.entityId);
                localBuffManager.Initialize(champ);
            }
        }

    }

    void ChampionStatsOnInsert(EventContext ctx, ChampionStats addedvalue)
    {
        Debug.Log(addedvalue);

    }

    private void ChampionInstanceOnUpdate(EventContext context, ChampionInstance oldRow, ChampionInstance newRow)
    {
        if(championControllers.TryGetValue(oldRow.EntityId, out ChampionController champController))
        {
            champController.UpdateChampion(newRow);
        }
    }

    private void WalkingOnUpdate(EventContext context, Walking oldRow, Walking newRow)
    {
        if (championControllers.TryGetValue(oldRow.EntityId, out ChampionController champController))
        {
            champController.UpdateWalker(newRow);
        }
    }

    private void WalkingOnDelete(EventContext context, Walking row)
    {
        if (championControllers.TryGetValue(row.EntityId, out ChampionController champController))
        {
            var entity = context.Db.Entity.EntityId.Find(row.EntityId);
            var pos = entity.Position;
            champController.DeleteWalker();
        }
    }

    private void WalkingOnInsert(EventContext ctx, Walking row)
    {
        if (championControllers.TryGetValue(row.EntityId, out ChampionController champController))
        {
            champController.UpdateWalker(row);
        }
    }
}
