using UnityEditor;
using UnityEngine;
//



namespace UnityTemplateProjects.Editor
{
    [CustomPropertyDrawer(typeof(Formula))]
    public class FormulaDrawer : PropertyDrawer
    {
        // Draw the property inside the given rect
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            // Using BeginProperty / EndProperty on the parent property means that
            // prefab override logic works on the entire property.
            EditorGUI.BeginProperty(position, label, property);

            if (property.objectReferenceValue)
            {
                var formulaObject = new SerializedObject(property.objectReferenceValue);
                // Draw label
                // EditorGUILayout.PrefixLabel(label);

                // Don't make child fields be indented
                // var indent = EditorGUI.indentLevel;

                // Draw fields - passs GUIContent.none to each so they are drawn without labels
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(formulaObject.FindProperty(nameof(Formula.Input)), label);
                if (EditorGUI.EndChangeCheck())
                {
                    Debug.Log("CHANGE");
                    formulaObject.ApplyModifiedProperties();
                    ((Formula) formulaObject.targetObject).OnEnable();
                    formulaObject.Update();
                }

                EditorGUI.indentLevel++;
                var paramsProp = formulaObject.FindProperty(nameof(Formula.Params));
                for (int i = 0; i < paramsProp.arraySize; i++)
                {
                    var elt = paramsProp.GetArrayElementAtIndex(i);
                    var nameProp = elt.FindPropertyRelative(nameof(FormulaParam.Name));
                    var valProp = elt.FindPropertyRelative(nameof(FormulaParam.Value));
                    EditorGUILayout.PropertyField(valProp, new GUIContent(nameProp.stringValue));
                }
                // EditorGUILayout.PropertyField(paramsProp, new GUIContent("Params"));

                // Set indent back to what it was
                // EditorGUI.indentLevel = indent;
                EditorGUI.indentLevel--;
            }
            else
                EditorGUILayout.PropertyField(property, label, true);

            EditorGUI.EndProperty();
        }
    }
}