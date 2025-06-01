using SpacetimeDB;
using SpacetimeDB.Types;
using System;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    const string SERVER_URL = "http://127.0.0.1:3000";
    const string MODULE_NAME = "moba";

    public static event Action OnConnected;
    public static event Action OnSubscriptionApplied;

    public static GameManager Instance { get; private set; }
    public static Identity LocalIdentity { get; private set; }
    public static DbConnection Conn { get; private set; }

    public Dictionary<uint, ChampionController> championInstances = new();

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
        conn.Db.ChampionInstance.OnInsert += ChampionInstanceOnInsert;
        conn.Db.ChampionInstance.OnUpdate += ChampionInstanceOnUpdate;
        conn.Db.Entity.OnUpdate += EntityOnUpdate;
        conn.Db.Actor.OnUpdate += ActorOnUpdate;
        conn.Db.Actor.OnInsert += ActorOnInsert;
        conn.Db.Walking.OnDelete += WalkingOnDelete;
        conn.Db.Walking.OnUpdate += WalkingOnUpdate;
        conn.Db.Walking.OnInsert += WalkingOnInsert;

        OnConnected?.Invoke();

        // Request all tables
        Conn.SubscriptionBuilder()
            .OnApplied(HandleSubscriptionApplied)
            .SubscribeToAllTables();
    }

    private void PlayerOnDelete(EventContext context, Player row)
    {
        Debug.Log("Player deleted");
    }
    private void ActorOnInsert(EventContext context, Actor row)
    {
        if (championInstances.TryGetValue(row.EntityId, out ChampionController champController))
        {
            champController.UpdateActor(row);
            Debug.Log("actor on Insert");
        }
        
    }

    private void ActorOnUpdate(EventContext context, Actor oldRow, Actor newRow)
    {
        if (championInstances.TryGetValue(oldRow.EntityId, out ChampionController champController))
        {
            champController.UpdateActor(newRow);
        }

        
    }

    private void EntityOnUpdate(EventContext context, Entity oldRow, Entity newRow)
    {
        if (championInstances.TryGetValue(oldRow.EntityId, out ChampionController champController))
        {
            champController.UpdateEntity(newRow);
        }
    }

    private void HandleSubscriptionApplied(SubscriptionEventContext ctx)
    {
        Debug.Log("Subscription applied!");
        OnSubscriptionApplied?.Invoke();
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
        Entity entity = ctx.Db.Entity.EntityId.Find(champ.EntityId);
        Actor actor = ctx.Db.Actor.EntityId.Find(champ.EntityId);

        championInstances.Add(champ.EntityId, PrefabManager.SpawnChampion(entity, actor, champ));
        Debug.Log("CHAMPION CREATED");
    }

    private void PlayerOnInsert(EventContext ctx, Player player)
    {
        Debug.Log($"Spawning player: {player.PlayerId}");
        PrefabManager.SpawnPlayer(player);
    }

    void ChampionStatsOnInsert(EventContext ctx, ChampionStats addedvalue)
    {
        Debug.Log(addedvalue);
    }

    private void ChampionInstanceOnUpdate(EventContext context, ChampionInstance oldRow, ChampionInstance newRow)
    {
        if(championInstances.TryGetValue(oldRow.EntityId, out ChampionController champController))
        {
            champController.UpdateChampion(newRow);
        }
    }

    private void WalkingOnUpdate(EventContext context, Walking oldRow, Walking newRow)
    {
        if (championInstances.TryGetValue(oldRow.EntityId, out ChampionController champController))
        {
            champController.UpdateWalker(newRow);
        }
    }

    private void WalkingOnDelete(EventContext context, Walking row)
    {
        if (championInstances.TryGetValue(row.EntityId, out ChampionController champController))
        {
            var entity = context.Db.Entity.EntityId.Find(row.EntityId);
            var pos = entity.Position;
            champController.UpdateWalker(new(entity.EntityId, entity.Position));
        }
    }

    private void WalkingOnInsert(EventContext ctx, Walking row)
    {
        if (championInstances.TryGetValue(row.EntityId, out ChampionController champController))
        {
            champController.UpdateWalker(row);
        }
    }
}
