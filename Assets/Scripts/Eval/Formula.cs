using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Eval.Runtime;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Eval
{
    /// <summary>
    /// The class to store in a monobehaviour. Minimal sample:
    /// <code>
    /// public class FormulaTest : MonoBehaviour
    /// {
    ///     public Formula Test;
    ///     private EvalGraph _evalgraph;
    ///   
    ///     public void Reset()
    ///     {
    ///         if (Test == null) Test = new Formula();
    ///         Test.SetParameters("t");
    ///     }
    ///   
    ///     private void Start() => Test.Compile(out _evalgraph);
    ///   
    ///       private void OnDestroy() =>_evalgraph.Dispose();
    ///   
    ///       private unsafe void Update()
    ///       {
    ///           #if UNITY_EDITOR
    ///           Test.LiveEdit(ref _evalgraph);
    ///           #endif
    ///           float3 t = Time.realtimeSinceStartup;
    ///           EvalState.Run(_evalgraph, &t, out float3 res);
    ///           transform.localPosition = res;
    ///       }
    /// }
    /// </code>
    /// </summary>
    [Serializable]
    public class Formula
    {

        [Delayed]
        public string Input;

        [SerializeField]
        private EvalGraph.Node[] Content;
        public List<FormulaParam> NamedValues;
        public List<string> Params;

        public void SetDirty() => _dirty = true;

        internal string _error;
        private int _lastFormulaHashCode;
        internal bool _dirty;


        public delegate void FormulaChangedCallback(EvalGraph oldGraph, EvalGraph newGraph);
        [Conditional("UNITY_EDITOR")]
        public void LiveEdit(ref EvalGraph evalGraph, FormulaChangedCallback onFormulaChanged = null)
        {
            if (_dirty)
            {
                _dirty = false;
                Init();
                
                _lastFormulaHashCode = Input?.GetHashCode() ?? 0;
            
                EvalGraph oldGraph = evalGraph;
                evalGraph = new EvalGraph(Content);
                onFormulaChanged?.Invoke(oldGraph, evalGraph);
                oldGraph.Dispose();
            }
        }


        public void Compile(out EvalGraph evalGraph)
        {
            if(Content == null)
                Init();

            // fixed (void* vptr = parsed)
            // {
            //     byte* bptr = (byte*) vptr;
            //     var byteLength = UnsafeUtility.SizeOf<EvalGraph.Node>() * parsed.Length;
            //     Content = new byte[byteLength];
            // }

            _lastFormulaHashCode = Input?.GetHashCode() ?? 0; 
            
            evalGraph = new EvalGraph(Content);
        }

        public void Init()
        {
            bool cleanup =
                    false
#if UNITY_EDITOR
                    || !UnityEditor.EditorApplication.isPlaying
#endif
                ;


            var root = Parser.Parse(Input, out _error);
            Debug.Log($"PARSING cleanup={cleanup} error={_error}");
            if (_error != null)
                return;
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

            Content = parsed;
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
        public bool IsSingleFloat; 

        public FormulaParam(string name, bool isSingleFloat = false)
        {
            Name = name;
            Value = default;
            IsSingleFloat = isSingleFloat;
        }
    }
}