using System;
using System.Collections.Generic;
using System.Linq;
using ShuntingYard;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace UnityTemplateProjects
{
    [Serializable]
    public class Formula
    {
        public static FormulaParamNameComparer s_ParamNameComparer = new FormulaParamNameComparer();
        public class FormulaParamNameComparer : IComparer<FormulaParam>
        {

            public int Compare(FormulaParam x, FormulaParam y) => string.Compare(x.Name, y.Name, StringComparison.Ordinal);
        }

        [Delayed]
        public string Input;
        public List<FormulaParam> NamedValues;
        public List<string> Params;
        public bool Dirty;

        private string _error;
        private int _lastFormulaHashCode;
        
        private void OnDisable()
        {
            _evalGraph.Dispose();
        }

        public void Init(bool force = false)
        {
            bool cleanup =
                force
#if UNITY_EDITOR
                || !UnityEditor.EditorApplication.isPlaying
#endif
                ;


            Debug.Log($"PARSING cleanup={cleanup}");
            var root = Parser.Parse(Input, out _error);
            _lastFormulaHashCode = Input?.GetHashCode() ?? 0; 
            if (root == null)
                return;
            var parsed = Translator.Translate(root, NamedValues, Params, out var usedValues);
            if (cleanup)
            {
                for (var index = NamedValues.Count - 1; index >= 0; index--)
                    if ((usedValues & (1ul << index)) == 0)
                        NamedValues.RemoveAt(index);
            }
            _lastGraphHash = EvalGraph.Hash(parsed);
            _dependency.Complete();
            _evalGraph.Dispose();
            _evalGraph = new EvalGraph(parsed);
        }

        private uint4 _lastGraphHash;
        private EvalGraph _evalGraph;
        private JobHandle _dependency;

        public void AddDependency(JobHandle h)
        {
            _dependency = JobHandle.CombineDependencies(_dependency, h);
        }
        public bool MakeEval(ref uint4 hash, ref EvalGraph evalGraph)
        {
            if(_lastFormulaHashCode == 0)
                Init();
            if ( !Dirty)
            {
                var changed = !hash.Equals(_lastGraphHash);
                hash = _lastGraphHash;
                evalGraph = _evalGraph;
                return changed;
            }
            
            Init();
            if (!_lastGraphHash.Equals(hash))
            {
                hash = _lastGraphHash;
                evalGraph = _evalGraph;
                return true;
            }

            return false;
        }

        public void SetParameters(params string[] formulaParams)
        {
            if (Params == null)
                Params = formulaParams.ToList(); 
            else
            {
                Params.Clear();
                Params.AddRange(formulaParams);
            }
        }
    }

    [Serializable]
    public struct FormulaParam
    {
        public string Name;
        public Vector3 Value;

        public FormulaParam(string name)
        {
            Name = name;
            Value = default;
        }
    }
}