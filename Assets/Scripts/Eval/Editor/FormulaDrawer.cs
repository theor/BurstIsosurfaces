using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.AI;

//



namespace UnityTemplateProjects.Editor
{
    [CustomPropertyDrawer(typeof(Formula))]
    public class FormulaDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            int i = 1; // input
            // if (property.propertyType == SerializedPropertyType.ManagedReference)
            // {
            //     var formulaObject = new SerializedObject(property.objectReferenceValue);
            var namedValues = property.FindPropertyRelative(nameof(Formula.NamedValues));
            i += namedValues.arraySize;
            var paramsProp = property.FindPropertyRelative(nameof(Formula.Params));
            i += paramsProp.arraySize;
            
            var formula = ((Formula) property.GetSerializedObject());
            if (!string.IsNullOrEmpty(formula._error))
                i++;
            // }
            // else
            //     return base.GetPropertyHeight(property, label);

            return EditorGUIUtility.singleLineHeight * i;
        }

        // Draw the property inside the given rect
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            void UpdateInstance()
            {
                var formula = ((Formula) property.GetSerializedObject());
                formula.Init();
                formula._dirty = true;
            }
            // Using BeginProperty / EndProperty on the parent property means that
            // prefab override logic works on the entire property.

                EditorGUI.BeginProperty(position, label, property);
                var formulaObject = property.serializedObject;
              
                var rect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight );
                    

                EditorGUI.BeginChangeCheck();
                EditorGUI.PropertyField(rect,
                    property.FindPropertyRelative(nameof(Formula.Input)), label); 
                if (EditorGUI.EndChangeCheck())
                {
                    Debug.Log("CHANGE");
                    formulaObject.ApplyModifiedProperties();
                    UpdateInstance();
                    formulaObject.Update();
                    // Debug.Log(EditorJsonUtility.ToJson(formulaObject.targetObject));
                }
                
                EditorGUI.indentLevel++;
                var e = ((Formula) property.GetSerializedObject())._error;
                if (!string.IsNullOrEmpty(e))
                {
                    rect.y += EditorGUIUtility.singleLineHeight;
                    var r = rect;
                    r.xMin += 16;
                    EditorGUI.HelpBox(r, e, MessageType.Error);
                }

                var namedValues = property.FindPropertyRelative(nameof(Formula.NamedValues));
                bool enabled = GUI.enabled;
                GUI.enabled = false;
                var paramsProp = property.FindPropertyRelative(nameof(Formula.Params));
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
                    var flagProp = elt.FindPropertyRelative(nameof(FormulaParam.IsSingleFloat));
                    var valueRect = rect;
                    var flagsRect = rect;
                    var flagPRopWidth = 100;
                    valueRect.xMax -= flagPRopWidth;
                    flagsRect.xMin = flagsRect.xMax - flagPRopWidth;
                    if(!flagProp.boolValue)
                        EditorGUI.PropertyField(valueRect, valProp, new GUIContent(nameProp.stringValue));
                    else
                    {
                        EditorGUI.PropertyField(valueRect, valProp.FindPropertyRelative(nameof(Vector3.x)),
                            new GUIContent(nameProp.stringValue));
                        // valProp.vector3Value = 
                    }

                    var w = EditorGUIUtility.labelWidth;
                    EditorGUIUtility.labelWidth = 50;
                    EditorGUI.PropertyField(flagsRect, flagProp, new GUIContent("Float"));
                    EditorGUIUtility.labelWidth = w;


                }

                if (EditorGUI.EndChangeCheck())
                {
                    formulaObject.ApplyModifiedProperties();
                    UpdateInstance();

                }
                EditorGUI.indentLevel--;

                EditorGUI.EndProperty();
        }
    }

    public static class DrawerExtensions
    {
        public static object GetSerializedObject(this SerializedProperty property)
        {
            return property.serializedObject.GetChildObject(property.propertyPath);
        }

        private static readonly Regex matchArrayElement = new Regex(@"^data\[(\d+)\]$");
        public static object GetChildObject(this SerializedObject serializedObject, string path)
        {
            object propertyObject = serializedObject.targetObject;

            if (path != "" && propertyObject != null)
            {
                string[] splitPath = path.Split('.');
                FieldInfo field = null;

                foreach (string pathNode in splitPath)
                {
                    if (field != null && field.FieldType.IsArray)
                    {
                        if (pathNode.Equals("Array"))
                            continue;

                        Match elementMatch = matchArrayElement.Match(pathNode);
                        int index;
                        if (elementMatch.Success && int.TryParse(elementMatch.Groups[1].Value, out index))
                        {
                            field = null;
                            object[] objectArray = (object[])propertyObject;
                            if (objectArray != null && index < objectArray.Length)
                                propertyObject = ((object[])propertyObject)[index];
                            else
                                return null;
                        }
                    }
                    else
                    {
                        field = propertyObject.GetType().GetField(pathNode, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        propertyObject = field.GetValue(propertyObject);
                    }
                }
            }

            return propertyObject;
        }
    }
}