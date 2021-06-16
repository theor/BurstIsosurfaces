using UnityEditor;
using UnityEngine;
//



namespace UnityTemplateProjects.Editor
{
    [CustomPropertyDrawer(typeof(Formula))]
    public class FormulaDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            int i = 1; // input

            if (property.objectReferenceValue)
            {
                var formulaObject = new SerializedObject(property.objectReferenceValue);
                var namedValues = formulaObject.FindProperty(nameof(Formula.NamedValues));
                i += namedValues.arraySize;
                var paramsProp = formulaObject.FindProperty(nameof(Formula.Params));
                i += paramsProp.arraySize;
            }

            return EditorGUIUtility.singleLineHeight * i;
        }

        // Draw the property inside the given rect
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            // Using BeginProperty / EndProperty on the parent property means that
            // prefab override logic works on the entire property.
            EditorGUI.BeginProperty(position, label, property);

            if (property.objectReferenceValue)
            {
                var formulaObject = new SerializedObject(property.objectReferenceValue);
              
                var rect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight );
                

                EditorGUI.BeginChangeCheck();
                EditorGUI.PropertyField(rect,
                    formulaObject.FindProperty(nameof(Formula.Input)), label); 
                if (EditorGUI.EndChangeCheck())
                {
                    Debug.Log("CHANGE");
                    formulaObject.ApplyModifiedProperties();
                    ((Formula) formulaObject.targetObject).Init(true);
                    formulaObject.Update();
                    Debug.Log(EditorJsonUtility.ToJson(formulaObject.targetObject));
                }

                EditorGUI.indentLevel++;
                var namedValues = formulaObject.FindProperty(nameof(Formula.NamedValues));
                bool enabled = GUI.enabled;
                GUI.enabled = false;
                var paramsProp = formulaObject.FindProperty(nameof(Formula.Params));
                for (int i = 0; i < paramsProp.arraySize; i++)
                {
                    var elt = paramsProp.GetArrayElementAtIndex(i);
                    rect.y += EditorGUIUtility.singleLineHeight;
                    EditorGUI.SelectableLabel(rect, elt.stringValue);
                }
                
                GUI.enabled = enabled;
                
                
                EditorGUI.BeginChangeCheck();
                for (int i = 0; i < namedValues.arraySize; i++)
                {
                    var elt = namedValues.GetArrayElementAtIndex(i);
                    rect.y += EditorGUIUtility.singleLineHeight;
                    var nameProp = elt.FindPropertyRelative(nameof(FormulaParam.Name));
                    var valProp = elt.FindPropertyRelative(nameof(FormulaParam.Value));
                    EditorGUI.PropertyField(rect, valProp, new GUIContent(nameProp.stringValue));
                    

                }

                if (EditorGUI.EndChangeCheck())
                {
                    formulaObject.ApplyModifiedProperties();
                    ((Formula) formulaObject.targetObject).Init(false);
                }
                EditorGUI.indentLevel--;
            }
            else
                EditorGUILayout.PropertyField(property, label, true);

            EditorGUI.EndProperty();
        }
    }
}