using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine.Assertions;

namespace UnityTemplateProjects
{
    [BurstCompile]
    public struct EvalState
    {
        private NativeList<float3> _stack;
        
        private int _current;
        
        private float3 Pop()
        {
            var elt = _stack[_stack.Length - 1];
            _stack.RemoveAt(_stack.Length-1);
            return elt;
        }

        private void Push(float3 val)
        {
            _stack.Add(val);
        }
        
        [BurstCompile]
        public unsafe float3 Run(in EvalGraph graph,  float3* @params)
        {
            using (_stack = new NativeList<float3>(10, Allocator.Temp))
            {
                _current = 0;
                _stack.Clear();
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
                        case Op.Dist:
                            Push(math.distance(Pop(),Pop()));
                            break;
                        case Op.SqDist:
                            Push(math.distancesq(Pop(),Pop()));
                            break;
                        case Op.Fbm:
                            Push(Fbm.fbm(Pop(),Pop().x,(int) Pop().x,Pop().x));
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
                        case Op.V3:
                            Push(new float3(Pop().x, Pop().x, Pop().x));
                            break;
                        default:
                            throw new NotImplementedException(string.Format("{0}", node.Op));
                    }

                    _current++;
                }

                Assert.AreEqual(1, _stack.Length);
                return _stack[0];
            }
        }
        
    }
}