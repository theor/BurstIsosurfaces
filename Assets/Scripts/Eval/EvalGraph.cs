using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Assertions;

namespace UnityTemplateProjects
{
    public struct EvalGraph : IDisposable
    {
        public static unsafe uint4 Hash(Node[] nodes)
        {
            if(nodes == null || nodes.Length == 0)
                return uint4.zero;
            fixed (Node* p = nodes)
                return xxHash3.Hash128(p, UnsafeUtility.SizeOf<Node>() * nodes.Length);
        }
        public struct Node
        {
            public Op Op;
            public float3 Val;
            public byte Index;

            public Node(Op op, float3 val = default)
            {
                Assert.AreNotEqual(Op.Param, op);
                Op = op;
                Val = val;
                Index = 0;
            }

            public static Node Param(byte index) => new Node(Op.Param, index);

            private Node(Op op, byte index)
            {
                Op = op;
                Val = default;
                Index = index;
            }
        }
        [NativeDisableUnsafePtrRestriction]
        public unsafe Node* Nodes;
        public ushort Length;
        private Allocator _allocator;


        public unsafe EvalGraph(Node[] nodes, Allocator allocator = Allocator.Persistent)
        {
            var size = (ushort) (UnsafeUtility.SizeOf<Node>() * nodes.Length);
            Length = (ushort) nodes.Length;
            _allocator = allocator;
            Nodes = (Node*) UnsafeUtility.Malloc(size, UnsafeUtility.AlignOf<Node>(),
                _allocator);
            fixed(Node* ptr = nodes)
                UnsafeUtility.MemCpy(Nodes, ptr, size);
        }

        public unsafe void Dispose()
        {
            if(Nodes != null)
                UnsafeUtility.Free(Nodes, _allocator);
        }
    }

    public enum Op
    {
        None,
        Const,
        Param,
        Add,Sub,
        Mul,Div,
        Mod,
        Minus,
        Abs,
        Saturate,
        X,Y,Z,
        Sin,Cos,Tan,
        CNoise,
        SNoise,
        SRDNoise,
        Fbm,
        Dist,
        SqDist,
        V3,
    }

    public static class Fbm
    {
        public static float fbm(float3 pos, float persistence, int octaves, float lacunarity)
        {
            float g = math.exp2(-persistence);
            float f = 1.0f;
            float a = 1.0f;
            float t = 0.0f;
            for (int i = 0; i < octaves; i++)
            {
                t += a * noise.snoise(f * pos);
                f *= lacunarity;
                a *= g;
            }

            return t;
        }
    }
}