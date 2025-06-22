using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;
using UnityEditor;
using SpacetimeDB.Types;

public class NavmeshBake : MonoBehaviour
{
    [MenuItem("Tools/Bake Navmesh")]
    public static void UploadNavmesh()
    {
        var data = NavMesh.CalculateTriangulation();

        var rawVertices = data.vertices.ToList();
        var indices = data.indices.ToList();

        List<DbVector2> uniqueVertices = new();
        Dictionary<int, int> vertexMapping = new(); // Maps old index to new index
        float epsilon = 0.01f; // Distance threshold for merging

        for (int i = 0; i < rawVertices.Count; i++)
        {
            Vector3 current = rawVertices[i];
            DbVector2 current2D = new(current.x, current.z);

            bool found = false;
            for (int j = 0; j < uniqueVertices.Count; j++)
            {
                if (Vector2.Distance(new Vector2((float)uniqueVertices[j].X, (float)uniqueVertices[j].Y), new Vector2(current2D.X, current2D.Y)) < epsilon)
                {
                    vertexMapping[i] = j; // Map old index to existing vertex
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                vertexMapping[i] = uniqueVertices.Count; // Map old index to new unique vertex
                uniqueVertices.Add(current2D);
            }
        }

        // Remap indices to use the new unique vertex indices
        List<int> newIndices = indices.Select(oldIndex => vertexMapping[oldIndex]).ToList();

        GameManager.Conn.Reducers.GenerateNavmeshFromClient(uniqueVertices, newIndices, 35);

        string vertexPrint = string.Join(", ", uniqueVertices);
        string indexPrint = string.Join(", ", newIndices);

        NavmeshVisualizer.instance.vertices = uniqueVertices.Select(v => new Vector3((float)v.X, 0, (float)v.Y)).ToList();
        NavmeshVisualizer.instance.indices = newIndices;
    }

    public static void WriteNavmeshToFile()
    {
        var data = NavMesh.CalculateTriangulation();

        var vertices = data.vertices.ToList();
        var indices = data.indices.ToList();

        string fileName = "bakedNavmesh.bytes";
        string filePath = Directory.GetParent(Directory.GetParent(Application.dataPath).FullName).FullName + "/server-csharp/navmesh";
        filePath = System.IO.Path.Combine(filePath, fileName);

        using (BinaryWriter writer = new BinaryWriter(File.Open(filePath, FileMode.Create)))
        {
            // Write vertex count
            writer.Write(vertices.Count);
            // Write each vertex as 3 floats
            foreach (var v in vertices)
            {
                writer.Write(v.x);
                writer.Write(v.y);
                writer.Write(v.z);
            }

            // Write index count
            writer.Write(indices.Count);
            // Write each index as int
            foreach (var i in indices)
            {
                writer.Write(i);
            }
        }

        Debug.Log($"Navmesh bytes written to {filePath}");

        //LoadNavmesh(filePath, out vertices, out indices);
    }
    public static void LoadNavmesh(string filePath, out List<DbVector2> vertices, out List<int> indices)
    {
        vertices = new List<DbVector2>();
        indices = new List<int>();
        string file = "navMesh/bakednavMesh";
        using (BinaryReader reader = new BinaryReader(File.Open(file, FileMode.Open)))
        {
            /* // Read vertex count
             int vertexCount = reader.ReadInt32();
             for (int i = 0; i < vertexCount; i++)
             {
                 float x = reader.ReadSingle();
                 float y = reader.ReadSingle();
                 float z = reader.ReadSingle();
                 vertices.Add(new DbVector2(x,z));
             }

             // Read index count
             int indexCount = reader.ReadInt32();
             for (int i = 0; i < indexCount; i++)
             {
                 indices.Add(reader.ReadInt32());
             }*/
        }

        Debug.Log($"Navmesh loaded. Vertices: {vertices.Count}, Indices: {indices.Count}");
    }

}
