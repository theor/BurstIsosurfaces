using System;
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
            EvalState.Run(_evalgraph, &t, out float3 res);
            transform.localPosition = res;
        }
    }
}