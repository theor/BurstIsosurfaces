using Unity.Mathematics;
using UnityEngine;

namespace UnityTemplateProjects
{
    public class FormulaTest : MonoBehaviour
    {
        public Formula Test;
        private uint4 _hash;
        private EvalGraph _evalgraph;

        public void Reset()
        {
            if (Test == null) Test = new Formula();
            Test.SetParameters("t");
        }

        private unsafe void Update()
        {
            Test.MakeEval(ref _hash, ref _evalgraph);
            float3 t = Time.realtimeSinceStartup;
            transform.localPosition = new EvalState().Run(_evalgraph, &t);
        }
    }
}