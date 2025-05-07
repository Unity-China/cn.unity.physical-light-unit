using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace UnityEditor.Rendering.Universal
{
    [CustomEditor(typeof(Exposure))]
    sealed class ExposureEditor : VolumeComponentEditor
    {
        SerializedDataParameter m_Mode;

        SerializedDataParameter m_FixedExposure;
        SerializedDataParameter m_Compensation;
        Material skyMat;
        Exposure exposureVolumeComponent;
        
        private static LightUnitSliderUIDrawer k_LightUnitSlider;
        
        public override void OnEnable()
        {
            var o = new PropertyFetcher<Exposure>(serializedObject);
            
            exposureVolumeComponent = serializedObject.targetObject as Exposure;
            
            m_Mode = Unpack(o.Find(x => x.mode));

            m_FixedExposure = Unpack(o.Find(x => x.fixedExposure));
            m_Compensation = Unpack(o.Find(x => x.compensation));
            
            k_LightUnitSlider = new LightUnitSliderUIDrawer();
        }

        public override void OnInspectorGUI()
        {
            PropertyField(m_Mode);

            int mode = m_Mode.value.intValue;
            if (mode == (int)ExposureMode.UsePhysicalCamera)
            {
                PropertyField(m_Compensation);
            }
            else if (mode == (int)ExposureMode.Fixed)
            {
                DoExposurePropertyField(m_FixedExposure);
                PropertyField(m_Compensation);
            }            
            
            // Skybox 
            skyMat = RenderSettings.skybox;
            EditorGUI.BeginChangeCheck();
            if(skyMat != null)
            {
                float ev100 = m_FixedExposure.value.floatValue;
                float comp = m_Compensation.value.floatValue; //TODO: for exposure not tfor skybox
                exposureVolumeComponent.SetSkyboxExposure(skyMat, ev100, comp + 15f);
            }
            if(EditorGUI.EndChangeCheck())
            {    
                m_FixedExposure.value.floatValue += m_Compensation.value.floatValue;
            }
            
        }
        
        // TODO: See if this can be refactored into a custom VolumeParameterDrawer
        void DoExposurePropertyField(SerializedDataParameter exposureProperty)
        {
            using (var scope = new OverridablePropertyScope(exposureProperty, exposureProperty.displayName, this))
            {
                if (!scope.displayed)
                    return;

                using (new EditorGUILayout.VerticalScope())
                {
                    EditorGUILayout.LabelField(scope.label);

                    var xOffset = EditorGUIUtility.labelWidth + 2;

                    var lineRect = EditorGUILayout.GetControlRect();
                    lineRect.x += xOffset;
                    lineRect.width -= xOffset;

                    var sliderRect = lineRect;
                    sliderRect.y -= EditorGUIUtility.singleLineHeight;
                    k_LightUnitSlider.SetSerializedObject(serializedObject);
                    k_LightUnitSlider.DrawExposureSlider(exposureProperty.value, sliderRect);

                    // GUIContent.none disables horizontal scrolling, use TrTextContent and adjust the rect to make it work.
                    lineRect.x -= EditorGUIUtility.labelWidth + 2;
                    lineRect.y += EditorGUIUtility.standardVerticalSpacing;
                    lineRect.width += EditorGUIUtility.labelWidth + 2;
                    EditorGUI.PropertyField(lineRect, exposureProperty.value, EditorGUIUtility.TrTextContent(" "));
                }
            }
        }
    }
}
