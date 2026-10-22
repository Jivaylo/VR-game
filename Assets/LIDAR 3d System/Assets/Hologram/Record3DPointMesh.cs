using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Mesh and bounds helpers shared by the Record3D point-cloud renderers
/// (<see cref="Record3DHologramRenderer"/> and <see cref="VolumetricHologramController"/>).
/// </summary>
public static class Record3DPointMesh
{
    /// <summary>
    /// Builds one quad per sampled texel. Each vertex stores (texel x, texel y, corner x, corner y);
    /// the shaders read the position and colour for that texel and expand the corner on screen.
    /// Samples sit at the centre of each step, so a fractional step (e.g. 3.75 colour pixels per
    /// LiDAR sample) picks exactly one texel per real depth sample.
    /// </summary>
    public static Mesh Build(int width, int height, int columns, int rows, float stepX, float stepY)
    {
        int points = columns * rows;
        int vertexCount = points * 4;
        int indexCount = points * 6;

        // Half floats represent the integer texel coordinates exactly up to 2048.
        bool useHalf = width <= 2048 && height <= 2048 && SystemInfo.SupportsVertexAttributeFormat(VertexAttributeFormat.Float16, 4);

        var mesh = new Mesh { name = "Record3D Hologram Points", hideFlags = HideFlags.DontSave };
        mesh.SetVertexBufferParams(vertexCount, new VertexAttributeDescriptor(VertexAttribute.Position, useHalf ? VertexAttributeFormat.Float16 : VertexAttributeFormat.Float32, 4));

        float[] cornersX = { -1f, 1f, 1f, -1f };
        float[] cornersY = { -1f, -1f, 1f, 1f };
        var indices = new int[indexCount];
        ushort[] halfVertices = useHalf ? new ushort[vertexCount * 4] : null;
        float[] floatVertices = useHalf ? null : new float[vertexCount * 4];

        int point = 0;
        for (int row = 0; row < rows; row++)
        {
            int texelY = Mathf.Min((int)((row + 0.5f) * stepY), height - 1);
            for (int column = 0; column < columns; column++, point++)
            {
                int texelX = Mathf.Min((int)((column + 0.5f) * stepX), width - 1);
                int firstVertex = point * 4;
                for (int corner = 0; corner < 4; corner++)
                {
                    int v = (firstVertex + corner) * 4;
                    if (useHalf)
                    {
                        halfVertices[v] = Mathf.FloatToHalf(texelX);
                        halfVertices[v + 1] = Mathf.FloatToHalf(texelY);
                        halfVertices[v + 2] = Mathf.FloatToHalf(cornersX[corner]);
                        halfVertices[v + 3] = Mathf.FloatToHalf(cornersY[corner]);
                    }
                    else
                    {
                        floatVertices[v] = texelX;
                        floatVertices[v + 1] = texelY;
                        floatVertices[v + 2] = cornersX[corner];
                        floatVertices[v + 3] = cornersY[corner];
                    }
                }

                int i = point * 6;
                indices[i] = firstVertex;
                indices[i + 1] = firstVertex + 1;
                indices[i + 2] = firstVertex + 2;
                indices[i + 3] = firstVertex;
                indices[i + 4] = firstVertex + 2;
                indices[i + 5] = firstVertex + 3;
            }
        }

        const MeshUpdateFlags flags = MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices;
        if (useHalf) mesh.SetVertexBufferData(halfVertices, 0, 0, halfVertices.Length, 0, flags);
        else mesh.SetVertexBufferData(floatVertices, 0, 0, floatVertices.Length, 0, flags);
        mesh.SetIndexBufferParams(indexCount, IndexFormat.UInt32);
        mesh.SetIndexBufferData(indices, 0, 0, indexCount, flags);
        mesh.subMeshCount = 1;
        mesh.SetSubMesh(0, new SubMeshDescriptor(0, indexCount, MeshTopology.Triangles), flags);
        // Culling uses RenderParams.worldBounds; this only keeps the mesh's own bounds sensible.
        mesh.bounds = new Bounds(new Vector3(0f, 0f, -2.5f), new Vector3(10f, 10f, 5f));
        mesh.UploadMeshData(true);
        return mesh;
    }

    /// <summary>The volume the recording camera's frustum covers between the clip depths, in local space.</summary>
    public static Bounds FrustumBounds(Record3DVideo video, float near, float far)
    {
        // Positions are x = (column - cx) * d / fx, y = (cy - row) * d / fy, z = -d.
        float fx = Mathf.Max(video.fx, 1e-3f);
        float fy = Mathf.Max(video.fy, 1e-3f);
        float halfWidth = Mathf.Max(video.tx, video.width - video.tx) / fx * far; // symmetric, so mirroring stays inside
        float top = video.ty / fy * far;
        float bottom = (video.height - video.ty) / fy * far;

        var bounds = new Bounds();
        bounds.SetMinMax(new Vector3(-halfWidth, -bottom, -far), new Vector3(halfWidth, top, -near));
        return bounds;
    }

    public static Bounds Transform(Bounds local, Matrix4x4 localToWorld)
    {
        var center = localToWorld.MultiplyPoint3x4(local.center);
        var extents = local.extents;
        var x = localToWorld.MultiplyVector(new Vector3(extents.x, 0f, 0f));
        var y = localToWorld.MultiplyVector(new Vector3(0f, extents.y, 0f));
        var z = localToWorld.MultiplyVector(new Vector3(0f, 0f, extents.z));
        var worldExtents = new Vector3(
            Mathf.Abs(x.x) + Mathf.Abs(y.x) + Mathf.Abs(z.x),
            Mathf.Abs(x.y) + Mathf.Abs(y.y) + Mathf.Abs(z.y),
            Mathf.Abs(x.z) + Mathf.Abs(y.z) + Mathf.Abs(z.z));
        return new Bounds(center, worldExtents * 2f);
    }
}
