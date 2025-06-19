using SpacetimeDB.Types;
using System.Collections.Generic;
using UnityEngine;

public class NavmeshVisualizer : MonoBehaviour
{
    public static NavmeshVisualizer instance;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else if (instance != this)
        {
            Debug.LogError("2 navmesh Visualizers in scene!");
            Destroy(this);
        }
    }

    public static Dictionary<uint,NavMeshVertex> vertices = new();
    public static Dictionary<uint, NavMeshEdge> edges = new();

    public static Dictionary<uint, NavMeshPolygon> polygons = new();

    public static Dictionary<uint, Path> paths = new();

    public static EntityController trackedEntity;

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;

        foreach (var polygon in polygons.Values)
        {
            List<NavMeshVertex> tempVertices = new();

            foreach (var vertexId in polygon.VertexIds)
            {
                NavMeshVertex vertex = vertices[vertexId];

                Gizmos.DrawSphere(new(vertex.Position.X, 0, vertex.Position.Y), 5f);

                foreach (var otherVertex in tempVertices)
                {
                    Gizmos.DrawLine(new(vertex.Position.X, 0, vertex.Position.Y), new(otherVertex.Position.X, 0, otherVertex.Position.Y));
                }

                tempVertices.Add(vertex);
            }

            Gizmos.DrawCube(new(polygon.Centroid.X, 0, polygon.Centroid.Y), Vector3.one * 10f);
        }


        Path lastPath = null;
        foreach (var path in paths.Values)
        {
            Gizmos.DrawSphere(new(path.Position.X, 0, path.Position.Y), 10f);

            if (lastPath != null)
            {
                Gizmos.DrawLine(new(path.Position.X, 0, path.Position.Y), new(lastPath.Position.X, 0, lastPath.Position.Y));
            }
            else if(trackedEntity != null)
            {
                Gizmos.DrawLine(new(path.Position.X, 0, path.Position.Y), new(trackedEntity.transform.position.x, 0, trackedEntity.transform.position.z));
            }

                lastPath = path;
        }
    }

}
