﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Unity.Mathematics;
using UnityEngine.Assertions;
using UnityTemplateProjects;

namespace ShuntingYard
{
    public enum OpType
    {
        Add,
        Sub,
        Mul,
        Div,
        LeftParens,
        RightParens,
        Plus,
        Minus,
        Coma,
        Mod
    }

   public interface INode
    {
    }

    interface IOp : INode
    {
    }

    interface IVal : INode
    {
    }

    struct UnOp : IOp
    {
        public readonly OpType Type;
        public readonly INode A;

        public UnOp(OpType type, INode a)
        {
            Type = type;
            A = a;
        }

        public override string ToString() => $"{Parser.Ops[Type].Str}{A}";
    }

    public struct BinOp : IOp
    {
        public readonly OpType Type;
        public readonly INode A;
        public readonly INode B;

        public BinOp(OpType type, INode a, INode b)
        {
            Type = type;
            A = a;
            B = b;
        }

        public override string ToString() => $"({A} {Parser.Ops[Type].Str} {B})";
    }

    struct FuncCall : IOp
    {
        public readonly string Id;
        public readonly List<INode> Arguments;

        public FuncCall(string id, List<INode> arguments)
        {
            Id = id;
            Arguments = arguments;
        }

        public override string ToString() => $"#{Id}({string.Join(", ", Arguments)})";
    }

    public struct ExpressionValue : IVal
    {
        public readonly float F;

        public ExpressionValue(float f)
        {
            F = f;
        }

        public override string ToString() => F.ToString(CultureInfo.InvariantCulture);
    }

    struct Variable : IVal
    {
        public readonly string Id;

        public Variable(string id)
        {
            Id = id;
        }

        public override string ToString() => $"${Id}";
    }

    [Flags]
    public enum Token
    {
        None = 0,
        Op = 1,
        Number = 2,
        Identifier = 4,
        LeftParens = 8,
        RightParens = 16,
        Coma = 32,
    }

    public static class Functions
    {
        public readonly struct FunctionDefinition
        {
            public readonly int ArgumentCount;
            public readonly Op OpCode;

            public FunctionDefinition(Op opCode, int argumentCount)
            {
                ArgumentCount = argumentCount;
                OpCode = opCode;
            }
        }

        private static Dictionary<string, List<FunctionDefinition>> _defs;
        public static bool TryGetOverloads(string functionId, out List<FunctionDefinition> overloads)
        {
            if (_defs == null)
                Init();
            return _defs.TryGetValue(functionId, out overloads);
        }

        
        private static void Init()
        {
            var ops = Enum.GetValues(typeof(Op)).Cast<Op>().ToArray();
            _defs = new Dictionary<string, List<FunctionDefinition>>(ops.Length, StringComparer.OrdinalIgnoreCase);
            foreach (Op op in ops)
            {
                if(op == Op.None)
                    continue;
                var str = op.ToString();
                int underscoreIndex = str.LastIndexOf('_');
                if(underscoreIndex < 0)
                    throw new InvalidDataException($"Operator {op} must be suffixed with an underscore and the number of expected parameters. If it takes 2 arguments, it must be named {op}_2.");
                var opName = str.Substring(0, underscoreIndex);
                string arityString = str.Substring(underscoreIndex + 1);
                if(!int.TryParse(arityString, out int arity))
                    throw new InvalidDataException($"Operator {op}'s argument count is not a valid int: '{arityString}'.");
                if(!_defs.TryGetValue(opName, out var defs))
                    _defs.Add(opName, defs = new List<FunctionDefinition>());
                defs.Add(new FunctionDefinition(op, arity));
            }
        }
    }

    public static class Translator
    {
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
                    nodes.Add(new EvalGraph.Node(Op.Const_0, v.F));
                    break;
                case Variable variable:
                    var paramIndex = formulaParams.IndexOf(variable.Id);
                    if(paramIndex >= 0)
                        nodes.Add(EvalGraph.Node.Param((byte) paramIndex));
                    else
                    {
                        var variableParam = new FormulaParam(variable.Id);
                        var idx = variables.BinarySearch(variableParam, Formula.s_ParamNameComparer);

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
                        nodes.Add(new EvalGraph.Node(Op.Const_0, variableParam.IsSingleFloat ? new float3(variableParam.Value.x) : (float3)variableParam.Value));
                    }

                    break;
                case UnOp u:
                    Rec(nodes, variables, u.A, formulaParams, ref usedValues);
                    if(u.Type == OpType.Plus)
                        break;
                    if(u.Type == OpType.Minus)
                        nodes.Add(new EvalGraph.Node(Op.Minus_1));
                    else
                        throw new NotImplementedException(u.Type.ToString());
                    break;
                case BinOp bin:
                    // reverse order
                    Rec(nodes, variables, bin.B, formulaParams, ref usedValues);
                    Rec(nodes, variables, bin.A, formulaParams, ref usedValues);
                    nodes.Add(new EvalGraph.Node(bin.Type switch
                    {
                        OpType.Add => Op.Add_2,
                        OpType.Sub => Op.Sub_2,
                        OpType.Mul => Op.Mul_2,
                        OpType.Div => Op.Div_2,
                        OpType.Mod => Op.Mod_2,
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
    public static class Parser
    {
        internal struct Operator
        {
            public readonly OpType Type;
            public readonly string Str;
            public readonly int Precedence;
            public readonly Associativity Associativity;
            public readonly bool Unary;

            public Operator(OpType type, string str, int precedence, Associativity associativity = Associativity.None,
                            bool unary = false)
            {
                Type = type;
                Str = str;
                Precedence = precedence;
                Associativity = associativity;
                Unary = unary;
            }
        }

        internal enum Associativity
        {
            None,
            Left,
            Right,
        }

        internal static readonly Dictionary<OpType, Operator> Ops = new Dictionary<OpType, Operator>
        {
            {OpType.Add, new Operator(OpType.Add, "+", 2, Associativity.Left)},
            {OpType.Sub, new Operator(OpType.Sub, "-", 2, Associativity.Left)},

            {OpType.Mul, new Operator(OpType.Mul, "*", 3, Associativity.Left)},
            {OpType.Div, new Operator(OpType.Div, "/", 3, Associativity.Left)},
            {OpType.Mod, new Operator(OpType.Mod, "%", 3, Associativity.Left)},

            {OpType.LeftParens, new Operator(OpType.LeftParens, "(", 5)},

            // {OpType.Coma, new Operator(OpType.Coma, ",", 1000, Associativity.Left)},

            // {OpType.Plus, new Operator(OpType.Plus, "+", 2000, Associativity.Right, unary: true)},
            {OpType.Minus, new Operator(OpType.Minus, "-", 2000, Associativity.Right, unary: true)},
        };

        static Operator ReadOperator(string input, bool unary)
        {
            return Ops.Single(o => o.Value.Str == input && o.Value.Unary == unary).Value;
        }

        public static INode Parse(string s, out string error)
        {
            if (s == null)
            {
                error = null;
                return null;
            }
            var output = new Stack<INode>();
            var opStack = new Stack<Operator>();

            Reader r = new Reader(s);

            try
            {
                r.ReadToken();
                error = null;
                return ParseUntil(r, opStack, output, Token.None, 0);
            }
            catch (Exception e)
            {
                error = e.Message;
                return null;
            }
        }

        public static bool TryPeek<T>(this Stack<T> stack, out T t)
        {
            if (stack.Count != 0)
            {
                t = stack.Peek();
                return true;
            }

            t = default;
            return false;
        }

        private static INode ParseUntil(Reader r, Stack<Operator> opStack, Stack<INode> output, Token readUntilToken,
            int startOpStackSize)
        {
            do
            {
                switch (r.CurrentTokenType)
                {
                    case Token.LeftParens:
                        {
                            opStack.Push(Ops[OpType.LeftParens]);
                            r.ReadToken();
                            INode arg = ParseUntil(r, opStack, output, Token.Coma | Token.RightParens,
                                opStack.Count);
                            if (r.CurrentTokenType == Token.Coma)
                                throw new InvalidDataException("Tuples not supported");
                            if (r.CurrentTokenType != Token.RightParens)
                                throw new InvalidDataException("Mismatched parens, missing a closing parens");
                            output.Push(arg);

                            while (opStack.TryPeek(out var stackOp) && stackOp.Type != OpType.LeftParens)
                            {
                                opStack.Pop();
                                PopOpOpandsAndPushNode(stackOp);
                            }

                            if (opStack.TryPeek(out var leftParens) && leftParens.Type == OpType.LeftParens)
                                opStack.Pop();
                            else
                                throw new InvalidDataException("Mismatched parens");
                            r.ReadToken();
                            break;
                        }
                    case Token.RightParens:
                        throw new InvalidDataException("Mismatched parens");
                    case Token.Op:
                        {
                            bool unary = r.PrevTokenType == Token.Op ||
                                r.PrevTokenType == Token.LeftParens ||
                                r.PrevTokenType == Token.None;
                            var readBinOp = ReadOperator(r.CurrentToken, unary);

                            while (opStack.TryPeek(out var stackOp) &&
                                   // the operator at the top of the operator stack is not a left parenthesis or coma
                                   stackOp.Type != OpType.LeftParens && stackOp.Type != OpType.Coma &&
                                   // there is an operator at the top of the operator stack with greater precedence
                                   (stackOp.Precedence > readBinOp.Precedence ||
                                    // or the operator at the top of the operator stack has equal precedence and the token is left associative
                                    stackOp.Precedence == readBinOp.Precedence &&
                                    readBinOp.Associativity == Associativity.Left))
                            {
                                opStack.Pop();
                                PopOpOpandsAndPushNode(stackOp);
                            }

                            opStack.Push(readBinOp);
                            r.ReadToken();
                            break;
                        }
                    case Token.Number:
                        output.Push(new ExpressionValue(float.Parse(r.CurrentToken, CultureInfo.InvariantCulture)));
                        r.ReadToken();
                        break;
                    case Token.Identifier:
                        var id = r.CurrentToken;
                        r.ReadToken();
                        if (r.CurrentTokenType != Token.LeftParens) // variable
                        {
                            output.Push(new Variable(id));
                            break;
                        }
                        else // function call
                        {
                            r.ReadToken(); // skip (
                            opStack.Push(Ops[OpType.LeftParens]);
                            List<INode> args = new List<INode>();

                            while (true)
                            {
                                INode arg = ParseUntil(r, opStack, output, Token.Coma | Token.RightParens,
                                    opStack.Count);
                                args.Add(arg);
                                if (r.CurrentTokenType == Token.RightParens)
                                {
                                    opStack.Pop();
                                    break;
                                }
                                r.ReadToken();
                            }

                            r.ReadToken(); // skip )

                            // RecurseThroughArguments(args, arg);
                            output.Push(new FuncCall(id, args));
                            break;
                        }
                    default:
                        throw new ArgumentOutOfRangeException(r.CurrentTokenType.ToString());
                }
            }
            while (!readUntilToken.HasFlag(r.CurrentTokenType));

            while (opStack.Count > startOpStackSize)
            {
                var readBinOp = opStack.Pop();
                if (readBinOp.Type == OpType.LeftParens)
                    break;
                PopOpOpandsAndPushNode(readBinOp);
            }

            return output.Pop();

            void PopOpOpandsAndPushNode(Operator readBinOp)
            {
                var b = output.Pop();
                if (readBinOp.Unary)
                {
                    output.Push(new UnOp(readBinOp.Type, b));
                }
                else
                {
                    if (output.Count == 0)
                        throw new InvalidDataException($"Missing operand for the {readBinOp.Str} operator in the expression");
                    var a = output.Pop();
                    output.Push(new BinOp(readBinOp.Type, a, b));
                }
            }

            void RecurseThroughArguments(List<INode> args, INode n)
            {
                switch (n)
                {
                    case BinOp b when b.Type == OpType.Coma:
                        RecurseThroughArguments(args, b.A);
                        RecurseThroughArguments(args, b.B);
                        break;
                    default:
                        args.Add(n);
                        break;
                }
            }
        }
    }

    public class Reader
    {
        private readonly string _input;
        private int _i;

        public Reader(string input)
        {
            _input = input.Trim();
            _i = 0;
        }

        private void SkipWhitespace()
        {
            while (!Done && Char.IsWhiteSpace(_input[_i]))
                _i++;
        }

        public bool Done => _i >= _input.Length;
        private char NextChar => _input[_i];
        private char ConsumeChar() => _input[_i++];

        public string CurrentToken;
        public Token CurrentTokenType;
        public Token PrevTokenType;

        public void ReadToken()
        {
            CurrentToken = null;
            PrevTokenType = CurrentTokenType;
            CurrentTokenType = Token.None;
            if (Done)
                return;
            if (NextChar == '(')
            {
                ConsumeChar();
                CurrentTokenType = Token.LeftParens;
            }
            else if (NextChar == ')')
            {
                ConsumeChar();
                CurrentTokenType = Token.RightParens;
            }
            else if (NextChar == ',')
            {
                ConsumeChar();
                CurrentTokenType = Token.Coma;
            }
            else if (Char.IsDigit(NextChar) || NextCharIsPoint())
            {
                bool foundPoint = false;
                StringBuilder sb = new StringBuilder();
                do
                {
                    foundPoint |= NextCharIsPoint();
                    sb.Append(ConsumeChar());
                }
                while (!Done && (Char.IsDigit(NextChar) || (NextChar == '.' && !foundPoint)));
                if (!Done && foundPoint && NextCharIsPoint()) // 1.2.3
                    throw new InvalidDataException($"Invalid number: '{sb}.'");

                CurrentToken = sb.ToString();
                CurrentTokenType = Token.Number;
            }
            else
            {
                if (MatchOp(out var op))
                {
                    CurrentToken = op.Str;
                    CurrentTokenType = Token.Op;
                    for (int i = 0; i < CurrentToken.Length; i++)
                        ConsumeChar();
                }
                else
                {
                    CurrentTokenType = Token.Identifier;
                    StringBuilder sb = new StringBuilder();
                    while (!Done && NextChar != ')' && NextChar != ',' && !MatchOp(out _) && !Char.IsWhiteSpace(NextChar))
                        sb.Append(char.ToLowerInvariant(ConsumeChar()));
                    CurrentToken = sb.ToString();
                }
            }

            SkipWhitespace();

            bool MatchOp(out Parser.Operator desc)
            {
                foreach (var pair in Parser.Ops)
                {
                    if (_input.IndexOf(pair.Value.Str, _i, StringComparison.Ordinal) != _i)
                        continue;
                    desc = pair.Value;
                    return true;
                }

                desc = default;
                return false;
            }

            bool NextCharIsPoint() => NextChar == '.';
        }
    }

    public static class Evaluator
    {
        public static float Eval(INode node, Dictionary<string, float> variables = null)
        {
            switch (node)
            {
                case ExpressionValue v:
                    return v.F;
                case Variable variable:
                    return variables[variable.Id];
                case UnOp u:
                    return u.Type == OpType.Plus ? Eval(u.A, variables) : -Eval(u.A, variables);
                case BinOp bin:
                    var a = Eval(bin.A, variables);
                    var b = Eval(bin.B, variables);
                    switch (bin.Type)
                    {
                        case OpType.Add:
                            return a + b;
                        case OpType.Sub:
                            return a - b;
                        case OpType.Mul:
                            return a * b;
                        case OpType.Div:
                            return a / b;
                        case OpType.Mod:
                            return a % b;
                        default:
                            throw new ArgumentOutOfRangeException(bin.Type.ToString());
                    }
                case FuncCall f:
                    void CheckArgCount(int n) => Assert.AreEqual(f.Arguments.Count, n);
                    switch (f.Id)
                    {
                        case "tan": return math.tan(Eval(f.Arguments.Single(), variables));
                        case "sin": return math.sin(Eval(f.Arguments.Single(), variables));
                        case "cos": return math.sin(Eval(f.Arguments.Single(), variables));
                        case "sqrt": return math.sqrt(Eval(f.Arguments.Single(), variables));
                        case "abs": return math.abs(Eval(f.Arguments.Single(), variables));
                        case "pow":
                            CheckArgCount(2);
                            return math.pow(Eval(f.Arguments[0], variables), Eval(f.Arguments[1], variables));
                        case "min":
                            CheckArgCount(2);
                            return math.min(Eval(f.Arguments[0], variables), Eval(f.Arguments[1], variables));
                        case "max":
                            CheckArgCount(2);
                            return math.max(Eval(f.Arguments[0], variables), Eval(f.Arguments[1], variables));
                        default: throw new InvalidDataException($"Unknown function {f.Id}");
                    }
    
                default: throw new NotImplementedException();
            }
        }
    }

    public static class Formatter
    {
        public static string Format(INode n)
        {
            switch (n)
            {
                case ExpressionValue v:
                    return v.F.ToString(CultureInfo.InvariantCulture);
                case Variable v:
                    return "$" + v.Id;
                case UnOp un:
                    return $"{FormatOp(un.Type)}{Format(un.A)}";
                case BinOp b:
                    return $"({Format(b.A)} {FormatOp(b.Type)} {Format(b.B)})";
                case FuncCall f:
                    var args = String.Join(", ", f.Arguments.Select(Format));
                    return $"{f.Id}({args})";
                default:
                    throw new NotImplementedException(n.ToString());
            }
        }

        private static string FormatOp(OpType bType)
        {
            return Parser.Ops[bType].Str;
        }
    }
}