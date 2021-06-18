using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using ShuntingYard;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Debug = UnityEngine.Debug;

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

        internal void SetDirty() => _dirty = true;

        private string _error;
        private int _lastFormulaHashCode;
        internal bool _dirty;


#if UNITY_EDITOR
        public delegate void FormulaChangedCallback(EvalGraph oldGraph, EvalGraph newGraph);
#endif
        [Conditional("UNITY_EDITOR")]
        public void LiveEdit(ref EvalGraph evalGraph, FormulaChangedCallback onFormulaChanged = null)
        {
            if (_dirty)
            {
                _dirty = false;
                var parsed =Init();
                
                _lastFormulaHashCode = Input?.GetHashCode() ?? 0;
            
                var newGraph = new EvalGraph(parsed);
                onFormulaChanged?.Invoke(evalGraph, newGraph);
                evalGraph.Dispose();

                evalGraph = newGraph;
            }
        }


        public void Compile(out EvalGraph evalGraph)
        {
            var parsed =Init();

            _lastFormulaHashCode = Input?.GetHashCode() ?? 0; 
            
            evalGraph = new EvalGraph(parsed);
        }

        public EvalGraph.Node[] Init()
        {
            bool cleanup =
                    false
#if UNITY_EDITOR
                    || !UnityEditor.EditorApplication.isPlaying
#endif
                ;


            Debug.Log($"PARSING cleanup={cleanup}");
            var root = Parser.Parse(Input, out _error);
            ulong usedValues;
            EvalGraph.Node[] parsed = null;
            if (root == null)
                usedValues = 0ul;
            else
                parsed = Translator.Translate(root, NamedValues, Params, out usedValues);
            if (cleanup)
            {
                for (var index = NamedValues.Count - 1; index >= 0; index--)
                    if ((usedValues & (1ul << index)) == 0)
                        NamedValues.RemoveAt(index);
            }

            return parsed;
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