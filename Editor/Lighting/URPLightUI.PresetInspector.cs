using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace UnityEditor.Rendering.Universal
{
    using CED = CoreEditorDrawer<SerializedHDLight>;

    internal partial class URPLightUI
    {
        static readonly ExpandedState<Expandable, Light> k_ExpandedStatePreset = new(0, "URP-preset");

        public static readonly CED.IDrawer PresetInspector = CED.Group(
            CED.Group((serialized, owner) =>
                EditorGUILayout.HelpBox(Rendering.LightUI.Styles.unsupportedPresetPropertiesMessage, MessageType.Info)),
            CED.Group((serialized, owner) => EditorGUILayout.Space()),
            CED.FoldoutGroup(Rendering.LightUI.Styles.generalHeader,
                Expandable.General,
                k_ExpandedStatePreset,
                DrawGeneralContentPreset),
            CED.FoldoutGroup(Rendering.LightUI.Styles.emissionHeader,
                Expandable.Emission,
                k_ExpandedStatePreset,
                CED.Group(
                    Rendering.LightUI.DrawColor,
                    DrawEmissionContent))
        );
    }
}
