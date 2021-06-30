using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine.Assertions;

namespace Eval.Runtime
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
        public static unsafe void Run(in EvalGraph graph, float3 singleParam, out float3 res)
        {
            Run(graph, &singleParam, out res);
        }
        [BurstCompile]
        public unsafe float3 Run(in EvalGraph graph,  float3* @params)
        {
            using (_stack = new NativeList<float3>(graph.MaxStackSize, Allocator.Temp))
            {
                _current = 0;
                _stack.Clear();
                while (_current < graph.Length)
                {
                    var node = graph.Nodes[_current];
                    switch (node.Op)
                    {
                        // unary
                        case EvalOp.Minus_1:
                            Push(-Pop());
                            break;
                        // no params
                        case EvalOp.Const_0:
                            Push(node.Val);
                            break;
                        case EvalOp.Param_0:
                            Push(@params[node.Index]);
                            break;
                        case EvalOp.Ld_0:
                            Push(_stack[node.Index-1]);
                            break;
                        
                        // binary and more
                        case EvalOp.Add_2:
                            Push(Pop() + Pop());
                            break;
                        case EvalOp.Sub_2:
                            Push(Pop() - Pop());
                            break;
                        case EvalOp.Div_2:
                            Push(Pop() / Pop());
                            break;
                        case EvalOp.Mul_2:
                            Push(Pop() * Pop());
                            break;
                        case EvalOp.Mod_2:
                            Push(math.fmod(Pop(), Pop()));
                            break;
                        case EvalOp.X_1:
                            Push(Pop().x);
                            break;
                        case EvalOp.Y_1:
                            Push(Pop().y);
                            break;
                        case EvalOp.Z_1:
                            Push(Pop().z);
                            break;
                        case EvalOp.Sin_1:
                            Push(math.sin(Pop()));
                            break;
                        case EvalOp.Cos_1:
                            Push(math.cos(Pop()));
                            break;
                        case EvalOp.Abs_1:
                            Push(math.abs(Pop()));
                            break;
                        case EvalOp.Saturate_1:
                            Push(math.saturate(Pop()));
                            break;
                        case EvalOp.Tan_1:
                            Push(math.tan(Pop()));
                            break;
                        case EvalOp.Dist_2:
                            Push(math.distance(Pop(),Pop()));
                            break;
                        case EvalOp.SqDist_2:
                            Push(math.distancesq(Pop(),Pop()));
                            break;
                        case EvalOp.Fbm_1:
                            Push(Fbm.fbm(Pop(),1,5,0.4f));
                            break;
                        case EvalOp.Fbm_4:
                            Push(Fbm.fbm(Pop(),Pop().x,(int) Pop().x,Pop().x));
                            break;
                        case EvalOp.CNoise_1:
                            Push(noise.cnoise(Pop()));
                            break;
                        case EvalOp.SNoise_1:
                            Push(noise.snoise(Pop()));
                            break;
                        case EvalOp.SRDNoise_1:
                            var float3 = Pop();
                            Push(noise.srdnoise(float3.xy, float3.z));
                            break;
                        case EvalOp.V3_3:
                            Push(new float3(Pop().x, Pop().x, Pop().x));
                            break;
                        case EvalOp.Box_2:
                            var p = Pop();
                            var b = Pop();
                            var q = math.abs(p) - b;
                            Push(math.length(math.max(q,0)) + math.min(math.max(q.x, math.max(q.y, q.z)), 0));
                            break;
                        default:
                            throw new NotImplementedException(string.Format("Operator {0} is not implemented", node.Op));
                    }

                    _current++;
                }

                Assert.AreNotEqual(0, _stack.Length);
                Assert.AreEqual(graph.ExpectedFinalStackSize, _stack.Length);
                return _stack[_stack.Length-1];
            }
        }
        
    }
}