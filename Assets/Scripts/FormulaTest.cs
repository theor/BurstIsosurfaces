using System;
using System.Diagnostics;
using Eval;
using Eval.Runtime;
using Unity.Mathematics;
using UnityEngine;

namespace UnityTemplateProjects
{
    public class FormulaTest : MonoBehaviour
    {
        public Formula Test;
        private EvalGraph _evalgraph;

        public void Reset()
        {
            if (Test == null) Test = new Formula();
            Test.SetParameters("t");
        }

        private void Start()
        {
            Test.Compile(out _evalgraph);
        }

        private void OnDestroy()
        {
            _evalgraph.Dispose();
        }

        private unsafe void Update()
        {
#if UNITY_EDITOR
            Test.LiveEdit(ref _evalgraph);
#endif
            float3 t = Time.realtimeSinceStartup;
            float3 res = float3.zero;
            // Stopwatch sw = Stopwatch.StartNew();
            // for (int i = 0; i < 100000; i++)
            {
                // res = new float3(math.cos(t * 7), math.sin(t * 7), 0);
                EvalState.Run(_evalgraph, &t, out res);
            }

            // var ms = sw.ElapsedMilliseconds;
            // Debug.Log($"{ms}ms");
            transform.localPosition = res;
        }
    }
}