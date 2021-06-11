using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Unity.Mathematics;
using UnityEngine.Assertions;

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
    }

    public struct Value : IVal
    {
        public readonly float F;

        public Value(float f)
        {
            F = f;
        }
    }

    struct Variable : IVal
    {
        public readonly string Id;

        public Variable(string id)
        {
            Id = id;
        }
    }

    public enum Token
    {
        None,
        Op,
        Number,
        Identifier,
        LeftParens,
        RightParens,
    }

    public static class Evaluator
    {
        public static float Eval(INode node, Dictionary<string, float> variables = null)
        {
            switch (node)
            {
                case Value v:
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
                case Value v:
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
                    throw new NotImplementedException();
            }
        }

        private static string FormatOp(OpType bType)
        {
            return Parser.Ops[bType].Str;
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

            {OpType.Coma, new Operator(OpType.Coma, ",", 1000)},

            {OpType.Plus, new Operator(OpType.Plus, "+", 2000, Associativity.Right, unary: true)},
            {OpType.Minus, new Operator(OpType.Minus, "-", 2000, Associativity.Right, unary: true)},
        };

        static Operator ReadOperator(string input, bool unary)
        {
            return Ops.Single(o => o.Value.Str == input && o.Value.Unary == unary).Value;
        }

        public static INode Parse(string s)
        {
            var output = new Stack<INode>();
            var opStack = new Stack<Operator>();

            Reader r = new Reader(s);
            r.ReadToken();

            return ParseUntil(r, opStack, output, Token.None, 0);
        }

        private static INode ParseUntil(Reader r, Stack<Operator> opStack, Stack<INode> output, Token readUntilToken,
            int startOpStackSize)
        {
            do
            {
                switch (r.CurrentTokenType)
                {
                    case Token.LeftParens:
                        opStack.Push(Ops[OpType.LeftParens]);
                        r.ReadToken();
                        break;
                    case Token.RightParens:
                    {
                        while (opStack.TryPeek(out var stackOp) && stackOp.Type != OpType.LeftParens)
                        {
                            opStack.Pop();
                            PopOpOpandsAndPushNode(stackOp);
                        }

                        if (opStack.TryPeek(out var leftParens) && leftParens.Type == OpType.LeftParens)
                            opStack.Pop();
                        r.ReadToken();
                        break;
                    }
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
                        output.Push(new Value(float.Parse(r.CurrentToken)));
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
                            INode arg = ParseUntil(r, opStack, output, Token.RightParens, opStack.Count);
                            r.ReadToken(); // skip )

                            List<INode> args = new List<INode>();
                            RecurseThroughArguments(args, arg);
                            output.Push(new FuncCall(id, args));
                            break;
                        }
                    default:
                        throw new ArgumentOutOfRangeException(r.CurrentTokenType.ToString());
                }
            } while (r.CurrentTokenType != readUntilToken);

            while (opStack.Count > startOpStackSize)
            {
                var readBinOp = opStack.Pop();
                if (readBinOp.Type == OpType.LeftParens || readBinOp.Type == OpType.RightParens)
                    throw new InvalidDataException("Mismatched parens");
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
            else if (Char.IsDigit(NextChar))
            {
                StringBuilder sb = new StringBuilder();
                do
                {
                    sb.Append(ConsumeChar());
                } while (!Done && Char.IsDigit(NextChar));

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
                    while (!Done && !Char.IsDigit(NextChar) && !MatchOp(out _))
                        sb.Append(ConsumeChar());
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
        }
    }

    static class Ext
    {
        public static bool TryPeek<T>(this Stack<T> stack, out T res)
        {
            if (stack.Count > 0)
            {
                res = stack.Peek();
                return true;
            }

            res = default;
            return false;
        }
    }
}