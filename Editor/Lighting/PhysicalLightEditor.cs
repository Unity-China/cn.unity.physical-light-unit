using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace UnityEditor.Rendering.Universal
{
    [CanEditMultipleObjects]
    [CustomEditorForRenderPipeline(typeof(Light), typeof(UniversalRenderPipelineAsset))]
    class PhysicalLightEditor : LightEditor
    {
        SerializedHDLight m_SerializedHDLight;

        AdditionalLightData[] m_AdditionalLightDatas;

        protected override void OnEnable()
        {
            base.OnEnable();

            // Get & automatically add additional HD data if not present
            m_AdditionalLightDatas = CoreEditorUtils.GetAdditionalData<AdditionalLightData>(targets, AdditionalLightData.InitDefaultHDAdditionalLightData);
            m_SerializedHDLight = new SerializedHDLight(m_AdditionalLightDatas, settings);

            ApplyAdditionalComponentsVisibility(true); 

            Undo.undoRedoPerformed += ReconstructReferenceToAdditionalDataSO;
        }

        internal void ReconstructReferenceToAdditionalDataSO()
        {
            OnDisable();
            OnEnable();
            
            // Serialized object is lossing references after an undo
            if (m_SerializedHDLight.serializedObject.targetObject != null)
                m_SerializedHDLight.serializedObject.Update();
        }
        
        void OnDisable()
        {
            Undo.undoRedoPerformed -= ReconstructReferenceToAdditionalDataSO;
        }

        // IsPreset is an internal API - lets reuse the usable part of this function
        // 93 is a "magic number" and does not represent a combination of other flags here
        internal static bool IsPresetEditor(UnityEditor.Editor editor)
        {
            return (int)((editor.target as Component).gameObject.hideFlags) == 93;
        }
        
        public override void OnInspectorGUI()
        {
            m_SerializedHDLight.Update();
            // Remove space before the first collapsible area
            EditorGUILayout.Space(-5);

            EditorGUI.BeginChangeCheck();

            if (IsPresetEditor(this))
            {
                URPLightUI.PresetInspector.Draw(m_SerializedHDLight, this);
            }
            else
            {
                using (new EditorGUILayout.VerticalScope())
                    URPLightUI.Inspector.Draw(m_SerializedHDLight, this);
            }
            if (EditorGUI.EndChangeCheck())
            {
                m_SerializedHDLight.Apply();

                foreach (var hdLightData in m_AdditionalLightDatas)
                {
                    hdLightData.UpdateAllLightValues();
                }
            } 
        }

        // Internal utilities
        void ApplyAdditionalComponentsVisibility(bool hide)
        {
            // Force to hide the UniversalAdditionalLightData component
            var data = CoreEditorUtils.GetAdditionalData<UniversalAdditionalLightData>(targets);
            if (data != null && hide)
            {
                foreach (var d in data)
                    d.hideFlags = HideFlags.HideInInspector;
            }
        }

        protected override void OnSceneGUI()
        {
            if (!(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset))
                return;

            if (!(target is Light light) || light == null)
                return;

            switch (light.type)
            {
                case LightType.Spot:
                    using (new Handles.DrawingScope(Matrix4x4.TRS(light.transform.position, light.transform.rotation, Vector3.one)))
                    {
                        CoreLightEditorUtilities.DrawSpotLightGizmo(light);
                    }
                    break;

                case LightType.Point:
                    using (new Handles.DrawingScope(Matrix4x4.TRS(light.transform.position, Quaternion.identity, Vector3.one)))
                    {
                        CoreLightEditorUtilities.DrawPointLightGizmo(light);
                    }
                    break;

                case LightType.Rectangle:
                    using (new Handles.DrawingScope(Matrix4x4.TRS(light.transform.position, light.transform.rotation, Vector3.one)))
                    {
                        CoreLightEditorUtilities.DrawRectangleLightGizmo(light);
                    }
                    break;

                case LightType.Disc:
                    using (new Handles.DrawingScope(Matrix4x4.TRS(light.transform.position, light.transform.rotation, Vector3.one)))
                    {
                        CoreLightEditorUtilities.DrawDiscLightGizmo(light);
                    }
                    break;

                case LightType.Directional:
                    using (new Handles.DrawingScope(Matrix4x4.TRS(light.transform.position, light.transform.rotation, Vector3.one)))
                    {
                        CoreLightEditorUtilities.DrawDirectionalLightGizmo(light);
                    }
                    break;

                default:
                    base.OnSceneGUI();
                    break;
            }
        }
    }
}
