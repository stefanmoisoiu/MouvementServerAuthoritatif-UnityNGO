using UnityEngine;
using UnityEditor;
using System.Reflection;

#if UNITY_EDITOR
namespace Physics.Editor
{
    [CustomEditor(typeof(PlayerPhysicsCalculator))]
    public class PlayerPhysicsCalculatorEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawDefaultInspector();

            EditorGUILayout.Space();

            if (GUILayout.Button("BakePhysicsComponents"))
            {
                PlayerPhysicsCalculator calculator = target as PlayerPhysicsCalculator;
                if (calculator != null)
                {
                    Undo.RecordObject(calculator, "Bake Physics Components");

                    MethodInfo method = calculator.GetType().GetMethod(
                        "BakePhysicsComponents",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                    if (method != null)
                    {
                        method.Invoke(calculator, null);
                        EditorUtility.SetDirty(calculator);
                        Debug.Log("BakePhysicsComponents executed.");
                    }
                    else
                    {
                        Debug.LogWarning("Method BakePhysicsComponents not found on " + calculator.GetType().Name);
                    }
                }
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif