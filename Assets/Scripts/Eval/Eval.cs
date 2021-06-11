using System;
using System.Diagnostics.Eventing.Reader;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.Assertions;

namespace UnityTemplateProjects
{
    [BurstCompile]
    public struct EvalJob : IJob
    {
        public Eval Eval;
        public NativeReference<float3> Result;
        public void Execute()
        {
            Result.Value = Eval.Run();
        }
    }
    
    [BurstCompile]
    public struct Eval : IDisposable
    {
        public struct Node
        {
            public Op Op;
            public float3 Val;
            public int Index;

            public Node(Op op, float3 val = default)
            {
                Op = op;
                Val = val;
                Index = 0;
            }

            public Node(Op op, int index)
            {
                Op = op;
                Val = default;
                Index = index;
            }
        }
        public static void F(){}

        public NativeArray<Node> Nodes;
        public NativeArray<float3> Params;
        
        public NativeList<float3> Stack;
        private int _current;

        public Eval(Node[] nodes, float3[] @params)
        {
            Nodes = new NativeArray<Node>(nodes, Allocator.Persistent);
            Params = new NativeArray<float3>(@params, Allocator.Persistent);
            Stack = new NativeList<float3>(10, Allocator.Persistent);
            _current = 0;
        }

        private float3 Pop()
        {
            var elt = Stack[Stack.Length - 1];
            Stack.RemoveAt(Stack.Length-1);
            return elt;
        }

        private void Push(float3 val)
        {
            Stack.Add(val);
        }
        [BurstCompile]
        public float3 Run()
        {
            _current = 0;
            Stack.Clear();
            while (_current < Nodes.Length)
            {
                var node = Nodes[_current];
                switch (node.Op)
                {
                    case Op.Const:
                        Push(node.Val);
                        break;
                    case Op.Param:
                        Push(Params[node.Index]);
                        break;
                    case Op.Add:
                        Push(Pop() + Pop());
                        break;
                    case Op.Sub:
                        Push(Pop() - Pop());
                        break;
                    case Op.Div:
                        Push(Pop() / Pop());
                        break;
                    case Op.Mul:
                        Push(Pop() * Pop());
                        break;
                    case Op.Mod:
                        Push(Pop() % Pop());
                        break;
                    case Op.X:
                        Push(Pop().x);
                        break;
                    case Op.Y:
                        Push(Pop().y);
                        break;
                    case Op.Z:
                        Push(Pop().z);
                        break;
                    case Op.Sin:
                        Push(math.sin(Pop()));
                        break;
                    case Op.Cos:
                        Push(math.cos(Pop()));
                        break;
                    case Op.Tan:
                        Push(math.tan(Pop()));
                        break;
                    case Op.CNoise:
                        Push( noise.cnoise(Pop()));
                        break;
                    case Op.SNoise:
                        Push( noise.snoise(Pop()));
                        break;
                    case Op.SRDNoise:
                        var float3 = Pop();
                        Push( noise.srdnoise(float3.xy, float3.z));
                        break;
                    default:
                        throw new NotImplementedException(string.Format("{0}", node.Op));
                }

                _current++;
            }

            Assert.AreEqual(1, Stack.Length);
            return Stack[0];
        }

        public void Dispose() => Dispose(default);
        public void Dispose(JobHandle handle)
        {
            if (Nodes.IsCreated)
            {
                Nodes.Dispose(handle);
                Stack.Dispose(handle);
                Params.Dispose(handle);
            }
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
        X,Y,Z,
        Sin,Cos,Tan,
        CNoise,
        SNoise,
        SRDNoise
    }
}