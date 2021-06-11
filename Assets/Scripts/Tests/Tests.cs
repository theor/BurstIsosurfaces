using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using NUnit.Framework;

namespace ShuntingYard
{
    class Tests
    {
        [Test]
        public void Test()
        {
            Console.WriteLine(Formatter.Format(new BinOp(OpType.Add,
                new BinOp(OpType.Mul, new Value(1), new Value(2)), new Value(3))));
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
        [TestCase("1,2", "(1 , 2)")]
        [TestCase("1,2,3", "(1 , (2 , 3))")]
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
        public void Parse(string input, string expectedFormat, float? result = null)
        {
            INode parsed = Parser.Parse(input);
            var format = Formatter.Format(parsed);
            Console.WriteLine(format);
            Assert.AreEqual(expectedFormat, format);
            if(result.HasValue)
                Assert.AreEqual(result.Value, Evaluator.Eval(parsed, new Dictionary<string, float>{{"a", 7f}}));
        }
        
        [TestCase("32+4", "32 + 4")]
        [TestCase("32+ 4", "32 + 4")]
        [TestCase("32+ 4*1", "32 + 4 * 1")]
        [TestCase("32+ 4*a+2", "32 + 4 * a + 2")]
        [TestCase("1*a", "1 * a")]
        [TestCase("(32+4)", "( 32 + 4 )")]
        [TestCase("(32+4)*1", "( 32 + 4 ) * 1")]
        [TestCase("1,2", "1 , 2")]
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
}