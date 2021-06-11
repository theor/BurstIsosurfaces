using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ShuntingYard;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityTemplateProjects;
using Assert = UnityEngine.Assertions.Assert;

public class ParsingEvaluationTests : EvaluationTestsBase
{
    static IEnumerable<TestCaseData> Cases
    {
        get
        {
            TestCaseData F(float3 a, string s, params float3[] @params) => new TestCaseData(a, s, @params);
            yield return F(1, "1");
            yield return F(3, "1+2");
            yield return F(3, "1+x", 2);
            yield return F(-1, "x(a) - 2", new float3(1,2,3));
            yield return F(1, "z(a) - 2", new float3(1,2,3));
        }
    }

    [TestCaseSource(nameof(Cases))]
    public void ParseRunTest(float3 result, string input, float3[] @params) => ParseRun(result, input, new Dictionary<string, int>(), @params);

    [Test]
    public void PreserveParamsMultipleExecutions()
    {
        var variables = new Dictionary<string, int>();
        ParseRun(1, "a", variables, 1,2);
        ParseRun(2, "b", variables, 1,2);
    }
}

public class EvaluationTestsBase
{
    protected void Run(float3 result, IEnumerable<Eval.Node> nodes, params float3[] @params)
    {
        EvalJob j = default;
        try
        {
            j = new EvalJob
            {
                Eval = new Eval(nodes.ToArray(), @params),
                Result = new NativeReference<float3>(Allocator.TempJob)
            };
            j.Run();
        }
        catch (Exception)
        {
            j.Eval.Dispose();
            j.Result.Dispose();
            throw;
        }

        Debug.Log($"Result: {j.Result.Value}");
        Assert.AreEqual(result, j.Result.Value);
    }

    protected void ParseRun(float3 result, string input, Dictionary<string, int> variables, params float3[] @params)
    {
        var n = Parser.Parse(input, out var err);
        Assert.IsNull(err, err);
        var nodes = Translator.Translate(n, variables, @params);
        Debug.Log(string.Join("\n",variables.Select(x => $"{x.Key}: {x.Value}")));
        Run(result, nodes, @params);
    }
}

public class EvaluationTests : EvaluationTestsBase
{
    // A Test behaves as an ordinary method
    [Test]
    public void ConstFloat3()
    {
        Run(new float3(1, 2, 3), new[] {new Eval.Node(Op.Const, new float3(1, 2, 3))});
    }

    [Test]
    public void Params()
    {
        Run(new float3(1, 2, 3), new[]
        {
            new Eval.Node(Op.Param, 0),
            new Eval.Node(Op.Param, 1),
            new Eval.Node(Op.Add),
        }, new float3(1, 2, 0), new float3(0, 0, 3));
    }

    [Test]
    public void AddFloat3()
    {
        Run(new float3(5, 7, 9), new[]
        {
            new Eval.Node(Op.Const, new float3(1, 2, 3)),
            new Eval.Node(Op.Const, new float3(4, 5, 6)),
            new Eval.Node(Op.Add),
        });
    }

    [Test]
    public void Div()
    {
        Run(new float3(2), new[]
        {
            new Eval.Node(Op.Const, 3f),
            new Eval.Node(Op.Const, 6f),
            new Eval.Node(Op.Div),
        });
    }
}