using Eval.Runtime;
using UnityEditor;
using UnityEngine;

namespace Eval.Editor
{
    class FormulaWindow : EditorWindow
    {
        [MenuItem("Formula/Test window")]
        public static void Open()
        {
            GetWindow<FormulaWindow>().Show();
        }

        [SerializeField]
        private Formula Formula;

        private UnityEditor.Editor _e;
        private EvalGraph _evalgraph;

        private void OnEnable()
        {
            _e = UnityEditor.Editor.CreateEditor(this);
            if (Formula == null)
                Formula = new Formula();
            Formula.SetParameters("a");
        }

        private void OnGUI()
        {
            EditorGUIUtility.wideMode = true;
            _e.OnInspectorGUI();
            Formula.LiveEdit(ref _evalgraph);
            var c = Formula.Content;
            if(c != null)
                for (var i = 0; i < c.Length; i++)
                {
                    var node = c[i];
                    EditorGUILayout.LabelField(i.ToString(), node.ToString() ?? "null");
                }
        }

        private void OnDestroy()
        {
            _evalgraph.Dispose();
        }
    }
}