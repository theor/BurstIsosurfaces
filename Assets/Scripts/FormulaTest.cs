using System;
using Unity.Mathematics;
using UnityEngine;

namespace UnityTemplateProjects
{
    public class FormulaTest : MonoBehaviour
    {
        public Formula Test;
        // private uint4 _hash;
        private EvalGraph _evalgraph;

        public void Reset()
        {
            if (Test == null) Test = new Formula();
            Test.SetParameters("t");
        }

        private void Start()
        {
            Test.Compile(ref _evalgraph);
        }

        private void OnDestroy()
        {
            _evalgraph.Dispose();
        }

        private unsafe void Update()
        {
            // Test.Compile(ref _hash, ref _evalgraph);
            float3 t = Time.realtimeSinceStartup;
            EvalState.Run(_evalgraph, &t, out float3 res);
            transform.localPosition = res;
            // transform.localPosition =  new EvalState().Run(_evalgraph, &t);
        }
    }
}