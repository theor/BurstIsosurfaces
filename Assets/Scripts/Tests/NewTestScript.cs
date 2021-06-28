using System;
using System.Collections.Generic;
using System.Linq;
using Eval;
using Eval.Runtime;
using NUnit.Framework;
using ShuntingYard;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Assert = UnityEngine.Assertions.Assert;

public class ParsingEvaluationTests : EvaluationTestsBase
{
    static IEnumerable<TestCaseData> Cases
    {
        get
        {
            TestCaseData F(float3 a, string s, params (string,float3)[] @params) => new TestCaseData(a, s, @params).SetName($"{s} = {a} {string.Join(", ", @params)}");
            yield return F(1, "1");
            yield return F(3, "1+2");
            yield return F(3, "1+x", ("x",2));
            yield return F(-1, "x(a) - 2", ("a", new float3(1,2,3)));
            yield return F(1, "z(a) - 2", ("a",new float3(1,2,3)));
        }
    }

    [TestCaseSource(nameof(Cases))]
    public void ParseRunTest(float3 result, string input, (string,float3)[] @params) =>
        ParseRun(result, input, new Dictionary<string, float3>(), @params);

    [Test]
    public void PreserveParamsMultipleExecutions()
    {
        var variables = new Dictionary<string, float3>();
        ParseRun(1, "a", variables, ("a",1),("b",2));
        ParseRun(2, "b", variables, ("a",1),("b",2));
    }
}

public class EvaluationTestsBase
{
    protected unsafe void Run(float3 result, IEnumerable<EvalGraph.Node> nodes, params float3[] @params)
    {
        EvalJob j = default;
        try
        {
            fixed (float3* paramsPtr = @params)
            {
                j = new EvalJob
                {
                    EvalGraph = new EvalGraph(nodes.ToArray()),
                    Result = new NativeReference<float3>(Allocator.TempJob),
                    Params = paramsPtr,
                };
                j.Run();
            }

            Debug.Log($"Result: {j.Result.Value}");
            Assert.AreEqual(result, j.Result.Value);
        }
        finally
        {
            j.EvalGraph.Dispose();
            j.Result.Dispose();
        }
    }

    protected void ParseRun(float3 result, string input, Dictionary<string, float3> variables, params (string,float3)[] @params)
    {
        var n = Parser.Parse(input, out var err);
        Assert.IsNull(err, err);
        var nodes = Translator.Translate(n, variables.Select(x => new FormulaParam(x.Key){Value = x.Value}).ToList(), @params.Select(x => x.Item1).ToList(), out var usedValues);
        Debug.Log(string.Join("\n",variables.Select(x => $"{x.Key}: {x.Value}")));
        Run(result, nodes, @params.Select(x => x.Item2).ToArray());
    }
}

public class EvaluationTests : EvaluationTestsBase
{
    // A Test behaves as an ordinary method
    [Test]
    public void ConstFloat3()
    {
        Run(new float3(1, 2, 3), new[] {new EvalGraph.Node(EvalOp.Const_0, new float3(1, 2, 3))});
    }

    [Test]
    public void Params()
    {
        Run(new float3(1, 2, 3), new[]
        {
            EvalGraph.Node.Param(0),
            EvalGraph.Node.Param(1),
            new EvalGraph.Node(EvalOp.Add_2),
        }, new float3(1, 2, 0), new float3(0, 0, 3));
    }

    [Test]
    public void AddFloat3()
    {
        Run(new float3(5, 7, 9), new[]
        {
            new EvalGraph.Node(EvalOp.Const_0, new float3(1, 2, 3)),
            new EvalGraph.Node(EvalOp.Const_0, new float3(4, 5, 6)),
            new EvalGraph.Node(EvalOp.Add_2),
        });
    }

    [Test]
    public void Div()
    {
        Run(new float3(2), new[]
        {
            new EvalGraph.Node(EvalOp.Const_0, 3f),
            new EvalGraph.Node(EvalOp.Const_0, 6f),
            new EvalGraph.Node(EvalOp.Div_2),
        });
    }
}