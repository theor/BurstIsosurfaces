using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace UnityTemplateProjects.Editor
{
    [CustomEditor(typeof(MeshGen))]
    public class MeshGenEditor : UnityEditor.Editor
    {
        [SerializeField]
        private static bool _drawOne = true;
        [SerializeField]
        private static Vector3Int _voxelCoords;
        

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            _drawOne = EditorGUILayout.Toggle("Draw one voxel", _drawOne);

            {
                _voxelCoords = EditorGUILayout.Vector3IntField("Coords", _voxelCoords);
            }
        }


        [DrawGizmo(GizmoType.Selected)]
        static void DrawMeshGen(MeshGen meshGen, GizmoType type)
        {
            var v1 = meshGen.VoxelSide + 1;
            var v2 = meshGen.VoxelSide + 2;
            var v3 = meshGen.VoxelSide + 3;
            if (_drawOne)
            {
                if (_voxelCoords.x < -1 || _voxelCoords.y < -1 || _voxelCoords.z < -1 ||
                    _voxelCoords.x > meshGen.VoxelSide || _voxelCoords.y > meshGen.VoxelSide ||
                    _voxelCoords.z > meshGen.VoxelSide)
                    return;
                var delta = 1 / (float) meshGen.VoxelSide;
                Gizmos.DrawWireCube((Vector3)_voxelCoords * delta + Vector3.one* delta*.5f,  Vector3.one * delta*.9f);

                var i3Coords = new int3(_voxelCoords.x, _voxelCoords.y, _voxelCoords.z);
                MeshGen.GetCornerCoords(i3Coords, v3, out var coords);
                for (int i = 0; i < 8; i++)
                {
                    var indexToCoords = MeshGen.IndexToCoords(coords[i], v3);
                    GUIStyle style = new GUIStyle();
                    style.normal.textColor = Color.green;
                    Handles.Label((Vector3)(float3)indexToCoords * delta, $"{i} {coords[i]} {indexToCoords}", style);
                }
            }
            else
            {
                // for (int i = -meshGen.VoxelSide; i < 2*meshGen.VoxelSide; i++)
                for (int x = -1; x < v1; x++)
                for (int y = -1; y < v1; y++)
                for (int z = -1; z < v1; z++)
                {
                    var delta = 1 / (float) meshGen.VoxelSide;
                    Gizmos.DrawWireCube(((Vector3) _voxelCoords + new Vector3(x, y, z)) * delta + Vector3.one* delta*.5f,  Vector3.one * delta);
                    var i3Coords = new int3(x, y, z);
                    MeshGen.GetCornerCoords(i3Coords, v3, out var coords);
                    for (int i = 0; i < 8; i++)
                    {
                        var indexToCoords = MeshGen.IndexToCoords(coords[i], v3);
                        GUIStyle style = new GUIStyle();
                        style.normal.textColor = Color.green;
                        Handles.Label(_voxelCoords + (Vector3)(float3)indexToCoords * delta, $"{i} {coords[i]} {indexToCoords}", style);
                    }
                }
                
            }
        }

    }
}