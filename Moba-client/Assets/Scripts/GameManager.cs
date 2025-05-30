using SpacetimeDB;
using UnityEngine;
using SpacetimeDB.Types;
using System;
using System.Collections.Generic;

public class GameManager : MonoBehaviour
{
    const string SERVER_URL = "http://127.0.0.1:3000";
    const string MODULE_NAME = "moba";

    public static event Action OnConnected;
    public static event Action OnSubscriptionApplied;

    public static GameManager Instance { get; private set; }
    public static Identity LocalIdentity { get; private set; }
    public static DbConnection Conn { get; private set; }

    public Dictionary<int, ChampionController> championInstances = new();

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
        conn.Db.ChampionInstance.OnInsert += ChampionInstanceOnInsert;
        conn.Db.Player.OnInsert += PlayerOnInsert;
        conn.Db.ChampionInstance.OnUpdate += ChampionInstanceOnUpdate;

        OnConnected?.Invoke();

        // Request all tables
        Conn.SubscriptionBuilder()
            .OnApplied(HandleSubscriptionApplied)
            .SubscribeToAllTables();
    }

  

    private void HandleSubscriptionApplied(SubscriptionEventContext ctx)
    {
        Debug.Log("Subscription applied!");
        OnSubscriptionApplied?.Invoke();

        foreach(var champ in ctx.Db.ChampionStats.Iter())
        {
            Debug.Log($"{champ.Name} iterated");
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
        championInstances.Add(champ.InstanceId, PrefabManager.SpawnChampion(champ));
        Debug.Log("CHAMPION CREATED");
    }

    private void PlayerOnInsert(EventContext ctx, Player player)
    {
        PrefabManager.SpawnPlayer(player);
    }

    void ChampionStatsOnInsert(EventContext ctx, ChampionStats addedvalue)
    {
        Debug.Log(addedvalue);
    }

    private void ChampionInstanceOnUpdate(EventContext context, ChampionInstance oldRow, ChampionInstance newRow)
    {
        if(championInstances.TryGetValue(oldRow.InstanceId, out ChampionController champController))
        {
            champController.UpdateChampion(newRow);
        }
    }
}
