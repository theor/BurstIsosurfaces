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
        public static unsafe void Run(in EvalGraph graph, float3* @params, out float3 res)
        {
            res = new EvalState().Run(graph, @params);
        }

        [BurstCompile]
        public unsafe float3 Run(in EvalGraph graph,  float3* @params)
        {
            using (_stack = new NativeList<float3>(10, Allocator.Temp))
            {
                _current = 0;
                _stack.Clear();
                while (_current < graph.Length)
                {
                    var node = graph.Nodes[_current];
                    switch (node.Op)
                    {
                        case Op.Minus_1:
                            Push(-Pop());
                            break;
                        case Op.Const_0:
                            Push(node.Val);
                            break;
                        case Op.Param_0:
                            Push(@params[node.Index]);
                            break;
                        case Op.Add_2:
                            Push(Pop() + Pop());
                            break;
                        case Op.Sub_2:
                            Push(Pop() - Pop());
                            break;
                        case Op.Div_2:
                            Push(Pop() / Pop());
                            break;
                        case Op.Mul_2:
                            Push(Pop() * Pop());
                            break;
                        case Op.Mod_2:
                            Push(Pop() % Pop());
                            break;
                        case Op.X_1:
                            Push(Pop().x);
                            break;
                        case Op.Y_1:
                            Push(Pop().y);
                            break;
                        case Op.Z_1:
                            Push(Pop().z);
                            break;
                        case Op.Sin_1:
                            Push(math.sin(Pop()));
                            break;
                        case Op.Cos_1:
                            Push(math.cos(Pop()));
                            break;
                        case Op.Abs_1:
                            Push(math.abs(Pop()));
                            break;
                        case Op.Saturate_1:
                            Push(math.saturate(Pop()));
                            break;
                        case Op.Tan_1:
                            Push(math.tan(Pop()));
                            break;
                        case Op.Dist_2:
                            Push(math.distance(Pop(),Pop()));
                            break;
                        case Op.SqDist_2:
                            Push(math.distancesq(Pop(),Pop()));
                            break;
                        case Op.Fbm_4:
                            Push(Fbm.fbm(Pop(),Pop().x,(int) Pop().x,Pop().x));
                            break;
                        case Op.CNoise_1:
                            Push(noise.cnoise(Pop()));
                            break;
                        case Op.SNoise_1:
                            Push(noise.snoise(Pop()));
                            break;
                        case Op.SRDNoise_1:
                            var float3 = Pop();
                            Push(noise.srdnoise(float3.xy, float3.z));
                            break;
                        case Op.V3_3:
                            Push(new float3(Pop().x, Pop().x, Pop().x));
                            break;
                        default:
                            throw new NotImplementedException(string.Format("Operator {0} is not implemented", node.Op));
                    }

                    _current++;
                }

                Assert.AreEqual(1, _stack.Length);
                return _stack[0];
            }
        }
        
    }
}