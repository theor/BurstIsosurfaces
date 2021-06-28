using System;
using System.Globalization;
using System.Linq;

namespace Eval
{
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