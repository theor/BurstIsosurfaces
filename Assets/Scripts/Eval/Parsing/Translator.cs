using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Eval.Runtime;
using Unity.Mathematics;
using UnityEngine.Assertions;

namespace Eval
{
    /// <summary>
    /// Translates the AST to a flat array containing the reverse polish notation (RPN) of the expression
    /// if the expression is 1 * (2 + 3), the RPN is 1 2 3 + * 
    /// </summary>
    public static class Translator
    {
        private static FormulaParamNameComparer s_ParamNameComparer = new FormulaParamNameComparer();

        internal class FormulaParamNameComparer : IComparer<FormulaParam>
        {
            public int Compare(FormulaParam x, FormulaParam y) => string.Compare(x.Name, y.Name, StringComparison.Ordinal);
        }
        
        public static EvalGraph.Node[] Translate(INode node, List<FormulaParam> variables, List<string> parameters,
            out ulong usedValues)
        {
            List<EvalGraph.Node> nodes = new List<EvalGraph.Node>();
            usedValues = 0;
            Rec(nodes, variables, node, parameters, ref usedValues);
            return nodes.ToArray();
        }

        private static void Rec(List<EvalGraph.Node> nodes, List<FormulaParam> variables, INode node,
            List<string> formulaParams, ref ulong usedValues)
        {
            
            switch (node)
            {
                case ExpressionValue v:
                    nodes.Add(new EvalGraph.Node(EvalOp.Const_0, v.F));
                    break;
                case Variable variable:
                    var paramIndex = formulaParams.IndexOf(variable.Id);
                    if(paramIndex >= 0)
                        nodes.Add(EvalGraph.Node.Param((byte) paramIndex));
                    else
                    {
                        var variableParam = new FormulaParam(variable.Id);
                        var idx = variables.BinarySearch(variableParam, s_ParamNameComparer);

                        if (idx < 0)
                        {
                            variables.Insert(~idx, variableParam);
                            idx = ~idx;
                            var shiftMask = (~0ul) << idx;
                            var shiftedPart = (usedValues & shiftMask) << 1;
                            var rightMask = (1ul << idx) - 1;
                            usedValues = shiftedPart | (usedValues & rightMask);
                        }
                        else
                            variableParam = variables[idx];

                        usedValues |= 1ul << idx; 
                        nodes.Add(new EvalGraph.Node(EvalOp.Const_0, variableParam.IsSingleFloat ? new float3(variableParam.Value.x) : (float3)variableParam.Value));
                    }

                    break;
                case UnOp u:
                    Rec(nodes, variables, u.A, formulaParams, ref usedValues);
                    if(u.Type == OpType.Plus)
                        break;
                    if(u.Type == OpType.Minus)
                        nodes.Add(new EvalGraph.Node(EvalOp.Minus_1));
                    else
                        throw new NotImplementedException(u.Type.ToString());
                    break;
                case BinOp bin:
                    // reverse order
                    Rec(nodes, variables, bin.B, formulaParams, ref usedValues);
                    Rec(nodes, variables, bin.A, formulaParams, ref usedValues);
                    nodes.Add(new EvalGraph.Node(bin.Type switch
                    {
                        OpType.Add => EvalOp.Add_2,
                        OpType.Sub => EvalOp.Sub_2,
                        OpType.Mul => EvalOp.Mul_2,
                        OpType.Div => EvalOp.Div_2,
                        OpType.Mod => EvalOp.Mod_2,
                        _ => throw new NotImplementedException(bin.Type.ToString())
                    }));
                    break;
                case FuncCall f:
                    void CheckArgCount(int n, ref ulong argUsedValues)
                    {
                        Assert.AreEqual(f.Arguments.Count, n);
                        // reverse order
                        for (int i = n - 1; i >= 0; i--)
                            Rec(nodes, variables, f.Arguments[i], formulaParams, ref argUsedValues);
                    }
                
                    if(!Functions.TryGetOverloads(f.Id, out var overloads))
                        throw new InvalidDataException($"Unknown function {f.Id}");
                    var overloadIndex = overloads.FindIndex(o => o.ArgumentCount == f.Arguments.Count);
                    if(overloadIndex == -1)
                        throw new InvalidDataException($"Function {f.Id} expects {String.Join(" or ", overloads.Select(o => o.ArgumentCount).ToString())} arguments, got {f.Arguments.Count}");
                    var overload = overloads[overloadIndex];
                
                    CheckArgCount(overload.ArgumentCount, ref usedValues);
                    nodes.Add(new EvalGraph.Node(overload.OpCode));
                    break;

                default: throw new NotImplementedException(node.ToString());
            }
        }
    }
}