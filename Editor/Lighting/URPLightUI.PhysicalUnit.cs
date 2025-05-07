using System;
using System.Collections.Generic;
using System.Drawing;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace UnityEditor.Rendering.Universal
{
    internal partial class URPLightUI
    {
        enum AdditionalProperties
        {
            General = 1 << 0,
            Shape = 1 << 1,
            Emission = 1 << 2,
            Shadow = 1 << 3,
        }

        readonly static LightUnitSliderUIDrawer k_LightUnitSliderUIDrawer = new LightUnitSliderUIDrawer();

        static void UpdateLightIntensityUnit(SerializedHDLight serialized, Editor owner)
        {
            LightType lightType = serialized.type;
            // Box are local directional light
            if (lightType == LightType.Directional)
            {
                serialized.lightUnit.SetEnumValue((LightUnit)DirectionalLightUnit.Lux);
                // We need to reset luxAtDistance to neutral when changing to (local) directional light, otherwise first display value ins't correct
                serialized.luxAtDistance.floatValue = 1.0f;
            }
        }

        internal static LightUnit DrawLightIntensityUnitPopup(Rect rect, LightUnit value, LightType type)
        {
            switch (type)
            {
                case LightType.Directional:
                    return (LightUnit)EditorGUI.EnumPopup(rect, (DirectionalLightUnit)value);
                case LightType.Point:
                    return (LightUnit)EditorGUI.EnumPopup(rect, (PunctualLightUnit)value);
                case LightType.Spot:
                    // if (spotLightShape == SpotLightShape.Box)
                    //     return (LightUnit)EditorGUI.EnumPopup(rect, (DirectionalLightUnit)value);
                    // else
                        return (LightUnit)EditorGUI.EnumPopup(rect, (PunctualLightUnit)value);
                default:
                    return (LightUnit)EditorGUI.EnumPopup(rect, (AreaLightUnit)value);
            }
        }

        static void DrawLightIntensityUnitPopup(Rect rect, SerializedHDLight serialized, Editor owner)
        {
            LightUnit oldLigthUnit = serialized.lightUnit.GetEnumValue<LightUnit>();

            EditorGUI.BeginChangeCheck();

            EditorGUI.BeginProperty(rect, GUIContent.none, serialized.lightUnit);
            EditorGUI.showMixedValue = serialized.lightUnit.hasMultipleDifferentValues;
            var selectedLightUnit = DrawLightIntensityUnitPopup(rect, serialized.lightUnit.GetEnumValue<LightUnit>(), serialized.type);
            EditorGUI.showMixedValue = false;
            EditorGUI.EndProperty();

            if (EditorGUI.EndChangeCheck())
            {
                  ConvertLightIntensity(oldLigthUnit, selectedLightUnit, serialized, owner);
                serialized.lightUnit.SetEnumValue(selectedLightUnit);
            }
        }

        internal static void ConvertLightIntensity(LightUnit oldLightUnit, LightUnit newLightUnit, SerializedHDLight serialized, Editor owner)
        {
            serialized.intensity.floatValue = ConvertLightIntensity(oldLightUnit, newLightUnit, serialized, owner, serialized.intensity.floatValue);
        }

        internal static float ConvertLightIntensity(LightUnit oldLightUnit, LightUnit newLightUnit, SerializedHDLight serialized, Editor owner, float intensity)
        {
            Light light = (Light)owner.target;

            // For punctual lights
            LightType lightType = serialized.type;
            switch (lightType)
            {
                case LightType.Directional:
                case LightType.Point:
                case LightType.Spot:
                    // Lumen ->
                    if (oldLightUnit == LightUnit.Lumen && newLightUnit == LightUnit.Candela)
                        intensity = LightUtils.ConvertPunctualLightLumenToCandela(lightType, intensity, light.intensity, serialized.enableSpotReflector.boolValue);
                    else if (oldLightUnit == LightUnit.Lumen && newLightUnit == LightUnit.Lux)
                        intensity = LightUtils.ConvertPunctualLightLumenToLux(lightType, intensity, light.intensity, serialized.enableSpotReflector.boolValue,
                            serialized.luxAtDistance.floatValue);
                    else if (oldLightUnit == LightUnit.Lumen && newLightUnit == LightUnit.Ev100)
                        intensity = LightUtils.ConvertPunctualLightLumenToEv(lightType, intensity, light.intensity, serialized.enableSpotReflector.boolValue);
                    // Candela ->
                    else if (oldLightUnit == LightUnit.Candela && newLightUnit == LightUnit.Lumen)
                        intensity = LightUtils.ConvertPunctualLightCandelaToLumen(lightType, intensity, serialized.enableSpotReflector.boolValue,
                            light.spotAngle, serialized.aspectRatio.floatValue);
                    else if (oldLightUnit == LightUnit.Candela && newLightUnit == LightUnit.Lux)
                        intensity = LightUtils.ConvertCandelaToLux(intensity, serialized.luxAtDistance.floatValue);
                    else if (oldLightUnit == LightUnit.Candela && newLightUnit == LightUnit.Ev100)
                        intensity = LightUtils.ConvertCandelaToEv(intensity);
                    // Lux ->
                    else if (oldLightUnit == LightUnit.Lux && newLightUnit == LightUnit.Lumen)
                        intensity = LightUtils.ConvertPunctualLightLuxToLumen(lightType,  intensity, serialized.enableSpotReflector.boolValue,
                            light.spotAngle, serialized.aspectRatio.floatValue, serialized.luxAtDistance.floatValue);
                    else if (oldLightUnit == LightUnit.Lux && newLightUnit == LightUnit.Candela)
                        intensity = LightUtils.ConvertLuxToCandela(intensity, serialized.luxAtDistance.floatValue);
                    else if (oldLightUnit == LightUnit.Lux && newLightUnit == LightUnit.Ev100)
                        intensity = LightUtils.ConvertLuxToEv(intensity, serialized.luxAtDistance.floatValue);
                    // EV100 ->
                    else if (oldLightUnit == LightUnit.Ev100 && newLightUnit == LightUnit.Lumen)
                        intensity = LightUtils.ConvertPunctualLightEvToLumen(lightType, intensity, serialized.enableSpotReflector.boolValue,
                            light.spotAngle, serialized.aspectRatio.floatValue);
                    else if (oldLightUnit == LightUnit.Ev100 && newLightUnit == LightUnit.Candela)
                        intensity = LightUtils.ConvertEvToCandela(intensity);
                    else if (oldLightUnit == LightUnit.Ev100 && newLightUnit == LightUnit.Lux)
                        intensity = LightUtils.ConvertEvToLux(intensity, serialized.luxAtDistance.floatValue);
                    break;

                case LightType.Area:
                    if (oldLightUnit == LightUnit.Lumen && newLightUnit == LightUnit.Nits)
                        intensity = LightUtils.ConvertAreaLightLumenToLuminance(serialized.type, intensity, serialized.settings.areaSizeX.floatValue, serialized.settings.areaSizeY.floatValue);
                    if (oldLightUnit == LightUnit.Nits && newLightUnit == LightUnit.Lumen)
                        intensity = LightUtils.ConvertAreaLightLuminanceToLumen(serialized.type, intensity, serialized.settings.areaSizeX.floatValue, serialized.settings.areaSizeY.floatValue);
                    if (oldLightUnit == LightUnit.Nits && newLightUnit == LightUnit.Ev100)
                        intensity = LightUtils.ConvertLuminanceToEv(intensity);
                    if (oldLightUnit == LightUnit.Ev100 && newLightUnit == LightUnit.Nits)
                        intensity = LightUtils.ConvertEvToLuminance(intensity);
                    if (oldLightUnit == LightUnit.Ev100 && newLightUnit == LightUnit.Lumen)
                        intensity = LightUtils.ConvertAreaLightEvToLumen(serialized.type, intensity, serialized.settings.areaSizeX.floatValue, serialized.settings.areaSizeY.floatValue);
                    if (oldLightUnit == LightUnit.Lumen && newLightUnit == LightUnit.Ev100)
                        intensity = LightUtils.ConvertAreaLightLumenToEv(serialized.type, intensity, serialized.settings.areaSizeX.floatValue, serialized.settings.areaSizeY.floatValue);
                    break;

                default:
                case (LightType)(-1): // multiple different values
                    break;  // do nothing
            }

            return intensity;
        }

        static void DrawLightIntensityGUILayout(SerializedHDLight serialized, Editor owner)
        {
            // Match const defined in EditorGUI.cs
            const int k_IndentPerLevel = 15;

            const int k_ValueUnitSeparator = 2;
            const int k_UnitWidth = 100;

            float indent = k_IndentPerLevel * EditorGUI.indentLevel;

            Rect lineRect = EditorGUILayout.GetControlRect();
            Rect labelRect = lineRect;
            labelRect.width = EditorGUIUtility.labelWidth;

            // Expand to reach both lines of the intensity field.
            var interlineOffset = EditorGUIUtility.singleLineHeight + 2f;
            labelRect.height += interlineOffset;

            //handling of prefab overrides in a parent label
            GUIContent parentLabel = s_Styles.lightIntensity;
            parentLabel = EditorGUI.BeginProperty(labelRect, parentLabel, serialized.lightUnit);
            parentLabel = EditorGUI.BeginProperty(labelRect, parentLabel, serialized.intensity);
            {
                // Restore the original rect for actually drawing the label.
                labelRect.height -= interlineOffset;

                EditorGUI.LabelField(labelRect, parentLabel);
            }
            EditorGUI.EndProperty();
            EditorGUI.EndProperty();

            // Draw the light unit slider + icon + tooltip
            Rect lightUnitSliderRect = lineRect; // TODO: Move the value and unit rects to new line
            lightUnitSliderRect.x += EditorGUIUtility.labelWidth + k_ValueUnitSeparator;
            lightUnitSliderRect.width -= EditorGUIUtility.labelWidth + k_ValueUnitSeparator;

            var lightType = serialized.type;
            var lightUnit = serialized.lightUnit.GetEnumValue<LightUnit>();
            k_LightUnitSliderUIDrawer.SetSerializedObject(serialized.serializedObject);
            k_LightUnitSliderUIDrawer.Draw(lightType, lightUnit, serialized.intensity, lightUnitSliderRect, serialized, owner);
            
            // We use PropertyField to draw the value to keep the handle at left of the field
            // This will apply the indent again thus we need to remove it time for alignment
            Rect valueRect = EditorGUILayout.GetControlRect();
            labelRect.width = EditorGUIUtility.labelWidth;
            valueRect.width += indent - k_ValueUnitSeparator - k_UnitWidth;
            Rect unitRect = valueRect;
            unitRect.x += valueRect.width - indent + k_ValueUnitSeparator;
            unitRect.width = k_UnitWidth + .5f;

            // Draw the unit textfield
            EditorGUI.BeginChangeCheck();
            EditorGUI.PropertyField(valueRect, serialized.intensity, CoreEditorStyles.empty);
            DrawLightIntensityUnitPopup(unitRect, serialized, owner);

            if (EditorGUI.EndChangeCheck())
            {
                serialized.intensity.floatValue = Mathf.Max(serialized.intensity.floatValue, 0.0f);
            }
        }

        static void DrawEmissionContent(SerializedHDLight serialized, Editor owner)
        {
            LightType lightType = serialized.type;
            // SpotLightShape spotLightShape = serialized.spotLightShape.GetEnumValue<SpotLightShape>();
            LightUnit lightUnit = serialized.lightUnit.GetEnumValue<LightUnit>();

            if (lightType != LightType.Directional
                // Box are local directional light and shouldn't display the Lux At widget. It use only lux
                // && !(lightType == LightType.Spot && (spotLightShape == SpotLightShape.Box))
                && lightUnit == (LightUnit)PunctualLightUnit.Lux)
            {
                EditorGUI.indentLevel++;
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(serialized.luxAtDistance, s_Styles.luxAtDistance);
                if (EditorGUI.EndChangeCheck())
                {
                    serialized.luxAtDistance.floatValue = Mathf.Max(serialized.luxAtDistance.floatValue, 0.01f);
                }
                EditorGUI.indentLevel--;
            }

            if (lightType == LightType.Spot
                // Display reflector only when showing additional properties.
                && (lightUnit == (int)PunctualLightUnit.Lumen && k_AdditionalPropertiesState[AdditionalProperties.Emission]))
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(serialized.enableSpotReflector, s_Styles.enableSpotReflector);
                EditorGUI.indentLevel--;
            }

            if (lightType != LightType.Directional)
            {
                EditorGUI.BeginChangeCheck();
#if UNITY_2020_1_OR_NEWER
                serialized.settings.DrawRange();
#else
                serialized.settings.DrawRange(false);
#endif
                // Make sure the range is not 0.0
                serialized.settings.range.floatValue = Mathf.Max(0.001f, serialized.settings.range.floatValue);

                if (EditorGUI.EndChangeCheck())
                {
                    // For GI we need to detect any change on additional data and call SetLightDirty + For intensity we need to detect light shape change
                    SetLightsDirty(owner); // Should be apply only to parameter that's affect GI, but make the code cleaner
                }
            }

            serialized.settings.DrawBounceIntensity();

            // EditorGUI.BeginChangeCheck(); // For GI we need to detect any change on additional data and call SetLightDirty

            DrawLightCookieContent(serialized, owner); // URP Cookie

            // if (EditorGUI.EndChangeCheck())
            // {
            //     SetLightsDirty(owner); // Should be apply only to parameter that's affect GI, but make the code cleaner
            // }
        }
        
        // Display reflector only when showing additional properties.      
        static void DrawEmissionAdditionalContent(SerializedHDLight serialized, Editor owner){}
        
        static void SetLightsDirty(Editor owner)
        {
            foreach (Light light in owner.targets)
                light.SetLightDirty(); // Should be apply only to parameter that's affect GI, but make the code cleaner
        }
    }
}
