using System;
using System.Collections.Generic;
using System.Linq;
using ShuntingYard;
using Unity.Mathematics;
using UnityEngine;

namespace UnityTemplateProjects
{
    [Serializable]
    public class Formula : ScriptableObject
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

        private EvalGraph.Node[] Parsed;
        private string _error;
        private int _lastFormulaHashCode;
        public bool Dirty => _lastFormulaHashCode != (Input?.GetHashCode() ?? 0);

        public void OnEnable()
        {
            Init(true);
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
            Parsed = Translator.Translate(root, NamedValues, Params);
            if (cleanup)
            {
                // for (var index = 0; index < formulaParams.Count; index++)
                // {
                //     var p = formulaParams[index];
                //     int existingValueIndex = NamedValues.BinarySearch(p, s_ParamNameComparer);
                //     if (existingValueIndex >= 0)
                //     {
                //         p.Value = NamedValues[existingValueIndex].Value;
                //         formulaParams[index] = p;
                //     }
                // }
                //
                // NamedValues = formulaParams;
            }
            _lastGraphHash = EvalGraph.Hash(Parsed);
            if(_evalGraph.Nodes.IsCreated) 
                _evalGraph.Dispose();
            _evalGraph = new EvalGraph(Parsed);
        }

        private uint4 _lastGraphHash;
        private EvalGraph _evalGraph;
        public bool MakeEval(ref uint4 hash, ref EvalGraph evalGraph)
        {
            if (!Dirty)
            {
                var changed = !hash.Equals(_lastGraphHash);
                hash = _lastGraphHash;
                evalGraph = _evalGraph;
                return changed;
            }
            OnEnable();
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