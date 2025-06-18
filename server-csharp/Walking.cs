using SpacetimeDB;
using System;
public static partial class Module
{
    [Table(Name = "walking", Public = true)]
    public partial struct Walking
    {
        [PrimaryKey, Unique]
        public uint entity_id;
        public DbVector2 target_walk_pos;
    }

    [Table(Name = "set_walk_target_timer", Scheduled = nameof(SetFutureTargetWalkPos), ScheduledAt = nameof(scheduled_at))]
    public partial struct SetWalkTargetTimer
    {
        [PrimaryKey, AutoInc]
        public ulong scheduled_id;

        [Unique]
        public uint entity_id;
        public ScheduleAt scheduled_at;
        public DbVector2 position;
        public bool remove_other_actions;
    }

    [Table(Name = "nav_mesh_vertex", Public = true)]
    public partial struct NavMeshVertex(DbVector2 position)
    {
        [PrimaryKey, AutoInc]
        public uint vertex_id;

        public DbVector2 position = position;
    }

    [Table(Name = "nav_mesh_edge", Public = true)]
    public partial struct NavMeshEdge(uint from_id, uint to_id)
    {
        [PrimaryKey, AutoInc]
        public uint edge_id;

        public uint from_vertex_id = from_id;
        public uint to_vertex_id = to_id;
    }


    [Table(Name = "path", Public = true)]
    public partial struct Path
    {
        [PrimaryKey, AutoInc]
        public uint path_id;

        [SpacetimeDB.Index.BTree]
        public uint entity_id;

        public DbVector2 position;

        public uint order;
    }

    /// <summary>
    /// Creates path from entities position to the given position
    /// </summary>
    [Reducer]
    public static void CreatePath(ReducerContext ctx, Entity entity, DbVector2 position)
    {
        var startNode = FindNearestNode(ctx, entity.position);
        var endNode = FindNearestNode(ctx, position);

        var finalList = AStar(ctx, startNode.vertex_id, endNode.vertex_id);

        uint orderCount = 0;

        foreach (var pos in finalList)
        {
            ctx.Db.path.Insert(new()
            {
                entity_id = entity.entity_id,
                position = pos,
                order = orderCount,
            });

            orderCount++;
        }
    }

    [Reducer]
    public static void SetFutureTargetWalkPos(ReducerContext ctx, SetWalkTargetTimer caller)
    {
        SetTargetWalkPos(ctx, caller.entity_id, caller.position, caller.remove_other_actions);
    }

    [Reducer]
    public static void SetTargetWalkPos(ReducerContext ctx, uint entityId, DbVector2 position, bool removeOtherActions = true)
    {
        var nEntity = ctx.Db.entity.entity_id.Find(entityId);
        if (nEntity == null) return;
        Entity entity = nEntity.Value;

        if (entity.busy) return;

        ctx.Db.path.entity_id.Delete(entity.entity_id); // THIS PART IS TEMP
        CreatePath(ctx, entity, position);

        ctx.Db.set_walk_target_timer.entity_id.Delete(entityId);

        var nullableAttacking = ctx.Db.attacking.entity_id.Find(entityId);

        if (nullableAttacking != null)
        {
            float timeSinceAttackStarted = GetTimestampDifferenceInSeconds(nullableAttacking.Value.attack_start_time, ctx.Timestamp);

            var nullableActor = ctx.Db.actor.entity_id.Find(entityId);
            if (nullableActor == null) return;
            Actor actor = nullableActor.Value;

            var nStats = ctx.Db.actor_base_stats.actor_id.Find(actor.actor_id);
            if (nStats == null) return;
            ActorBaseStats stats = nStats.Value;

            float windupTime = (1f / stats.attack_speed) * stats.windup_percent;
            float timeUntilHit = windupTime - timeSinceAttackStarted;

            if (Math.Abs(timeUntilHit) < .4f * windupTime)
            {
                ctx.Db.set_walk_target_timer.Insert(new()
                {
                    scheduled_at = new Timestamp(nullableAttacking.Value.attack_start_time.MicrosecondsSinceUnixEpoch + (int)(windupTime * 1.4f * 1_000_000f)),
                    entity_id = entityId,
                    position = position,
                    remove_other_actions = removeOtherActions
                });
                return;
            }
        }

        if (removeOtherActions) ctx.Db.attacking.entity_id.Delete(entityId);


        var newWalking = new Walking()
        {
            entity_id = entityId,
            target_walk_pos = position
        };

        ctx.Db.walking.entity_id.Delete(entityId);
        ctx.Db.walking.Insert(newWalking);

    }

    [Reducer]
    public static void MoveActor(ReducerContext ctx, Walking walker, Entity entity)
    {
        float moveSpeed = 250; //Units per Second

        #region find entity and actor

        

        List<Path> paths = ctx.Db.path.entity_id.Filter(entity.entity_id).ToList();
        if (paths.Count == 0) return;

        paths.Sort((a, b) => a.order.CompareTo(b.order));


        var nullableUnit = ctx.Db.actor.entity_id.Find(entity.entity_id);
        if (nullableUnit == null)
        {
            ctx.Db.walking.entity_id.Delete(entity.entity_id);
            return;
        }
        Actor actor = nullableUnit.Value;
        #endregion

        #region movement math
        var difference = paths[0].position - entity.position;

        float distance = difference.Magnitude();

        float distanceToMove = moveSpeed * deltaTime.Microseconds / 1_000_000; //in seconds

        var direction = difference.Normalized();

        float velocity = (entity.last_position - entity.position).Magnitude();

        DbVector2 newPos;
        if (distance <= distanceToMove)
        {
            newPos = paths[0].position;
            if (paths.Count - 1 <= 0)
            {
                ctx.Db.walking.entity_id.Delete(entity.entity_id);
            }
            else
            {
                //target next point
                ctx.Db.path.path_id.Delete(paths[0].path_id);
            }
        }
        else
        {
            newPos = new(entity.position.x + (direction.x * distanceToMove), entity.position.y + (direction.y * distanceToMove));
        }

        float finalRotation = DbVector2.RotationFromDirection(direction);
        #endregion

        #region update entity and actor

        Actor newActor = actor;

        newActor.rotation = finalRotation;

        ctx.Db.actor.entity_id.Delete(entity.entity_id);
        ctx.Db.actor.Insert(newActor);


        DbVector2 newLastPos = entity.position;

        ctx.Db.entity.entity_id.Delete(entity.entity_id);
        ctx.Db.entity.Insert(new Entity()
        {
            entity_id = entity.entity_id,
            position = newPos,
            last_position = newLastPos,
            busy = false //walking isnt a busy action
        });
        #endregion
    }

    [Reducer]
    public static void MoveAllPlayers(ReducerContext ctx)
    {

        var list = ctx.Db.walking.Iter();
        foreach (var walker in list)
        {
            var nullableEntity = ctx.Db.entity.entity_id.Find(walker.entity_id);
            if (nullableEntity == null)
            {
                Log.Info("null entity, skipping walk and deleting it");
                ctx.Db.walking.entity_id.Delete(walker.entity_id);
                continue;
            }
            Entity entity = nullableEntity.Value;

            MoveActor(ctx, walker, entity);

        }
    }

    public static NavMeshVertex FindNearestNode(ReducerContext ctx, DbVector2 position)
    {
        NavMeshVertex nearestNode = default;
        float closestDistance = float.MaxValue;

        foreach (var node in ctx.Db.nav_mesh_vertex.Iter())
        {
            float distance = (node.position - position).Magnitude();
            if (distance < closestDistance)
            {
                closestDistance = distance;
                nearestNode = node;
            }
        }

        return nearestNode;
    }

    public static float Heuristic(ReducerContext ctx, uint fromVertexId, uint toVertexId)
    {
        var nFromNode = ctx.Db.nav_mesh_vertex.vertex_id.Find(fromVertexId);
        var nToNode = ctx.Db.nav_mesh_vertex.vertex_id.Find(toVertexId);

        if (nFromNode == null || nToNode == null)
        {
            return 0;
        }
        NavMeshVertex fromNode = nFromNode.Value;
        NavMeshVertex toNode = nToNode.Value;

        return (fromNode.position - toNode.position).Magnitude();
    }

    public static List<DbVector2> ReconstructPath(ReducerContext ctx, Dictionary<uint, uint> cameFrom, uint current)
    {
        var totalPath = new List<DbVector2>();

        while (cameFrom.ContainsKey(current))
        {
            var node = ctx.Db.nav_mesh_vertex.vertex_id.Find(current);
            current = cameFrom[current];
            if (node == null)
            {
                continue;
            }
            totalPath.Add(node.Value.position);
           
        }

        // Add start node
        var startNode = ctx.Db.nav_mesh_vertex.vertex_id.Find(current);
        if (startNode != null)
        {
            totalPath.Add(startNode.Value.position);
        }


        totalPath.Reverse(); // Path is from goal to start, so reverse it
        foreach (var node in totalPath)
        {
            Log.Info(node.ToString());
        }
        return totalPath;
    }

    public static List<DbVector2> AStar(ReducerContext ctx, uint startVertexId, uint goalVertexId)
    {
        // Open set: nodes to explore, sorted by fScore
        var openSet = new PriorityQueue<uint, float>();
        openSet.Enqueue(startVertexId, 0f);

        // CameFrom: tracks how we got to each node
        var cameFrom = new Dictionary<uint, uint>();

        // gScore: cost from start to current node
        var gScore = new Dictionary<uint, float>
        {
            [startVertexId] = 0f
        };

        // fScore: estimated total cost (gScore + heuristic)
        var fScore = new Dictionary<uint, float>
        {   
            [startVertexId] = Heuristic(ctx, startVertexId, goalVertexId)
        };

        while (openSet.Count > 0)
        {
            uint current = openSet.Dequeue();

            if (current == goalVertexId)
                return ReconstructPath(ctx, cameFrom, current);

            foreach (var edge in ctx.Db.nav_mesh_edge.Iter().Where(e => e.from_vertex_id == current))
            {
                uint neighbor = edge.to_vertex_id;
                float tentativeGScore = gScore[current] + 1; //Assume all edges cost 1 for now

                if (!gScore.ContainsKey(neighbor) || tentativeGScore < gScore[neighbor])
                {
                    cameFrom[neighbor] = current;
                    gScore[neighbor] = tentativeGScore;

                    float estimatedFScore = tentativeGScore + Heuristic(ctx, neighbor, goalVertexId);
                    fScore[neighbor] = estimatedFScore;

                    // If neighbor is not in the queue, add it
                    if (!openSet.UnorderedItems.Any(item => item.Element == neighbor))
                        openSet.Enqueue(neighbor, estimatedFScore);
                }
            }
        }

        // No path found
        return new List<DbVector2>();
    }

    [Reducer]
    public static void GenerateNavmesh(ReducerContext ctx)
    {
        NavMeshVertex v1 = new(new(700, 700));
        NavMeshVertex v2 = new(new(700, -700));
        NavMeshVertex v3 = new(new(-700, -700));
        NavMeshVertex v4 = new(new(-700, 700));

        v1 = ctx.Db.nav_mesh_vertex.Insert(v1);
        v2 = ctx.Db.nav_mesh_vertex.Insert(v2);
        v3 = ctx.Db.nav_mesh_vertex.Insert(v3);
        v4 = ctx.Db.nav_mesh_vertex.Insert(v4);

        NavMeshEdge e1 = new(v1.vertex_id, v2.vertex_id);
        NavMeshEdge e2 = new(v2.vertex_id, v3.vertex_id);
        NavMeshEdge e3 = new(v3.vertex_id, v4.vertex_id);
        NavMeshEdge e4 = new(v4.vertex_id, v1.vertex_id);

        ctx.Db.nav_mesh_edge.Insert(e1);
        ctx.Db.nav_mesh_edge.Insert(e2);
        ctx.Db.nav_mesh_edge.Insert(e3);
        ctx.Db.nav_mesh_edge.Insert(e4);
    }
}
