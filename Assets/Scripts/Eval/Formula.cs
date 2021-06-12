using System;
using System.Collections.Generic;
using System.Linq;
using ShuntingYard;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace UnityTemplateProjects
{
    [Serializable]
    public class Formula : ScriptableObject
    {
        [Delayed, Multiline]
        public string Input;
        public List<FormulaParam> Params;

        private Dictionary<string, byte> Variables;
        private EvalGraph.Node[] Parsed;
        private string _error;
        public bool Dirty { get; set; }

        public void OnEnable()
        {
            Dirty = true;
            bool cleanup =
            #if UNITY_EDITOR
                !UnityEditor.EditorApplication.isPlaying;
            #else
                false;
            #endif
            if (Variables == null)
            {
                Variables = new Dictionary<string, byte>();
                cleanup = true;
            }
            var root = Parser.Parse(Input, out _error);
            if (root == null)
                return;
            Parsed = Translator.Translate(root, Variables, null);
            foreach (var keyValuePair in Variables.OrderBy(x => x.Value))
            {
                while(Params.Count <= keyValuePair.Value)
                    Params.Add(default);
                var formulaParam = Params[keyValuePair.Value];
                formulaParam.Name = keyValuePair.Key;
                Params[keyValuePair.Value] = formulaParam;
            }

            if (cleanup)
            {
                Params.RemoveAll(p => !Variables.ContainsKey(p.Name));
                HashSet<string> names = new HashSet<string>();
                for (var i = 0; i < Params.Count; i++)
                {
                    if (!names.Add(Params[i].Name))
                    {
                        Params.RemoveAt(i--);
                    }
                }
            }
        }

        public bool MakeEval(ref uint4 hash, ref EvalGraph evalGraph)
        {
            OnEnable();
            var newHash = EvalGraph.Hash(Parsed);
            if (!newHash.Equals(hash))
            {
                hash = newHash;
                evalGraph = new EvalGraph(Parsed);
                 return true;
            }

            return false;
        }
    }

    [Serializable]
    public struct FormulaParam
    {
        public string Name;
        public Vector3 Value;
    }
}