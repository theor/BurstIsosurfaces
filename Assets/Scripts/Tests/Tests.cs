using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using Eval;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using Formatter = Eval.Formatter;

namespace ShuntingYard
{
    class Tests
    {
        

        [Test]
        public void Test()
        {
            Console.WriteLine(Formatter.Format(new BinOp(OpType.Add,
                new BinOp(OpType.Mul, new ExpressionValue(1), new ExpressionValue(2)), new ExpressionValue(3))));
        }

        [TestCase("3+4", "(3 + 4)", 7f)]
        [TestCase("12*34", "(12 * 34)", 12f*34f)]
        [TestCase("12*34+45", "((12 * 34) + 45)", 12*34+45f)]
        [TestCase("12+34*45", "(12 + (34 * 45))", 12+34*45f)]
        [TestCase("12+34+45", "((12 + 34) + 45)", 12+34+45f, Description = "Left associativity")]
        [TestCase("(32+4)", "(32 + 4)", 32+4f)]
        [TestCase("a", "$a", 7f)]
        [TestCase("1 * a+3", "((1 * $a) + 3)", 1*7+3f)]
        // unary
        [TestCase("-1", "-1", -1f)]
        [TestCase("--1", "--1", 1f)]
        [TestCase("-3+4", "(-3 + 4)", -3+4f)]
        [TestCase("3+-4", "(3 + -4)", 3+-4f)]
        [TestCase("-(3+4)", "-(3 + 4)", -(3+4f))]
        // coma
        // [TestCase("1,2", "(1 , 2)")]
        // [TestCase("1,2,3", "(1 , (2 , 3))")]
        // func calls
        [TestCase("sqrt(64)", "sqrt(64)", 8f)]
        [TestCase("min(42, 43)", "min(42, 43)", 42f)]
        [TestCase("abs(-42)", "abs(-42)", 42f)]
        [TestCase("sqrt(63+1)", "sqrt((63 + 1))", 8f)]
        [TestCase("sqrt(abs(-64))", "sqrt(abs(-64))", 8f)]
        [TestCase("max(1, sqrt(4))", "max(1, sqrt(4))", 2f)]
        [TestCase("max(-1, abs(-4))", "max(-1, abs(-4))", 4f)]
        [TestCase("abs(abs(1+1/2))", "abs(abs((1 + (1 / 2))))", 1.5f)]
        [TestCase("tan(1)", "tan(1)", 1.55740774f)]
        [TestCase("tan(tan(1))", "tan(tan(1))", 74.6860046f)]
        [TestCase("tan(tan(11%10))", "tan(tan((11 % 10)))", 74.6860046f)]
        [TestCase("dist(a, 0.5) - 0.3 / 0.01*snoise(a + fbm(a)) ", "(dist($a, 0.5) - ((0.3 / 0.01) * snoise(($a + fbm($a)))))", null)]
        [TestCase("1*abs(a + 2) ", "(1 * abs(($a + 2)))", 9f)]
        public void Parse(string input, string expectedFormat, float? result = null)
        {
            INode parsed = Parser.Parse(input,  out var err);
            if (!string.IsNullOrEmpty(err))
            {
                Debug.Log(err);
            }
            var format = Formatter.Format(parsed);
            Debug.Log(format);
            Assert.AreEqual(expectedFormat, format);
            if(result.HasValue)
                Assert.AreEqual(result.Value, Evaluator.Eval(parsed, new Dictionary<string, float> {{"a", 7f}}));
        }
        
        [TestCase("32+4", "32 + 4")]
        [TestCase("32+ 4", "32 + 4")]
        [TestCase("32+ 4*1", "32 + 4 * 1")]
        [TestCase("32+ 4*a+2", "32 + 4 * a + 2")]
        [TestCase("1*a", "1 * a")]
        [TestCase("(32+4)", "( 32 + 4 )")]
        [TestCase("(32+4)*1", "( 32 + 4 ) * 1")]
        // [TestCase("1,2", "1 , 2")]
        public void Tokenizer_Works(string input, string spaceSeparatedTokens)
        {
            var reader = new Reader(input);
            string result = null;
            while (!reader.Done)
            {
                reader.ReadToken();
                var readerCurrentToken = reader.CurrentTokenType == Token.LeftParens ? "(" : reader.CurrentTokenType == Token.RightParens ? ")" : reader.CurrentToken;
                if (result == null)
                    result = readerCurrentToken;
                else
                    result += " " + readerCurrentToken;
            }

            Console.WriteLine(result);
            Assert.AreEqual(spaceSeparatedTokens, result);
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

}