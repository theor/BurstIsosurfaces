using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
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
        [NativeDisableUnsafePtrRestriction]
        public unsafe float3* Params;
        public unsafe void Execute()
        {
            EvalState state = new EvalState();
            Result.Value = state.Run(Eval, Params);
        }
    }

    [BurstCompile]
    public struct EvalState
    {
        private NativeList<float3> Stack;
        
        private int _current;
        
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
        public unsafe float3 Run(in Eval graph,  float3* @params)
        {
            using (Stack = new NativeList<float3>(10, Allocator.Temp))
            {
                _current = 0;
                Stack.Clear();
                while (_current < graph.Nodes.Length)
                {
                    var node = graph.Nodes[_current];
                    switch (node.Op)
                    {
                        case Op.Const:
                            Push(node.Val);
                            break;
                        case Op.Param:
                            Push(@params[node.Index]);
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
                            Push(noise.cnoise(Pop()));
                            break;
                        case Op.SNoise:
                            Push(noise.snoise(Pop()));
                            break;
                        case Op.SRDNoise:
                            var float3 = Pop();
                            Push(noise.srdnoise(float3.xy, float3.z));
                            break;
                        default:
                            throw new NotImplementedException(string.Format("{0}", node.Op));
                    }

                    _current++;
                }

                Assert.AreEqual(1, Stack.Length);
                return Stack[0];
            }
        }
        
    }
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
        public NativeArray<Node> Nodes;
        

        public Eval(Node[] nodes, float3[] @params)
        {
            Nodes = new NativeArray<Node>(nodes, Allocator.Persistent);
        }

     

        public void Dispose() => Dispose(default);
        public void Dispose(JobHandle handle)
        {
            if (Nodes.IsCreated)
            {
                Nodes.Dispose(handle);
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