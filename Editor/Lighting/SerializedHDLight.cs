using UnityEngine;
using System.Linq;
using System.Collections.Generic;
using UnityEngine.Rendering.Universal;

namespace UnityEditor.Rendering.Universal
{
    internal class SerializedHDLight : ISerializedLight
    {
        // URP specific properties        
        // Combines the UniversalAdditionalLightData SerializedProperties to HighDefinitionLightData
        public UniversalAdditionalLightData additionalLightData => lightsAdditionalData[0];
        public UniversalAdditionalLightData[] lightsAdditionalData { get; }

        // URP Light Properties
        public SerializedProperty useAdditionalDataProp { get; } // Does light use shadow bias settings defined in UniversalRP asset file?
        public SerializedProperty additionalLightsShadowResolutionTierProp { get; } // Index of the AdditionalLights ShadowResolution Tier
        public SerializedProperty softShadowQualityProp { get; } // Per light soft shadow filtering quality.
        public SerializedProperty lightCookieSizeProp { get; } // Multi dimensional light cookie size replacing `cookieSize` in legacy light.
        public SerializedProperty lightCookieOffsetProp { get; } // Multi dimensional light cookie offset.

        // Light layers related
        public SerializedProperty renderingLayers { get; }
        public SerializedProperty customShadowLayers { get; }
        public SerializedProperty shadowRenderingLayers { get; }
        
        // Common properties 
        public SerializedProperty intensity { get; }

        // HDRP specific properties
        public SerializedProperty enableSpotReflector;
        public SerializedProperty luxAtDistance;

        public SerializedProperty aspectRatio;

        /// TODO: Add Volumetric UI if we has Volumetric fog
        public SerializedProperty volumetricDimmer;
        public SerializedProperty volumetricFadeDistance;
        public SerializedProperty lightUnit;

        /// TODO: Add Celestial Body UI if we has Physical Sky
        // Celestial Body
        public SerializedProperty interactsWithSky;
        public SerializedProperty angularDiameter;
        public SerializedProperty flareSize;
        public SerializedProperty flareTint;
        public SerializedProperty flareFalloff;
        public SerializedProperty surfaceTexture;
        public SerializedProperty surfaceTint;
        public SerializedProperty distance;
        
        // Editor stuff
        public SerializedProperty useVolumetric;

        // Shadow datas
        public SerializedProperty shadowDimmer;
        public SerializedProperty volumetricShadowDimmer;
        public SerializedProperty shadowFadeDistance;

        public SerializedObject serializedObject { get; }
        // This is Serialized UniversalAdditionalLightData for URP only, not in use for HighDefinition Pipeline,
        // However we will also use it here to include that necessary SerializedLight data in SerializedHDLight
        public SerializedObject serializedAdditionalDataObject { get; private set;}

        private SerializedObject lightGameObject;

        //contain serialized property that are mainly used to draw inspector
        public LightEditor.Settings settings { get; }

        //type is converted on the fly each time so we cannot have SerializedProperty on it
        public LightType type
        {
            get => haveMultipleTypeValue
                ? (LightType)(-1)     //as serialize property on enum when mixed value state happens
                : (serializedObject.targetObjects[0] as AdditionalLightData).type;
            set
            {
                //Note: type is split in both component
                var undoObjects = serializedObject.targetObjects.SelectMany((Object x) => new Object[] { x, (x as AdditionalLightData).legacyLight }).ToArray();
                Undo.RecordObjects(undoObjects, "Change light type");
                var objects = serializedObject.targetObjects;
                for (int index = 0; index < objects.Length; ++index)
                    (objects[index] as AdditionalLightData).type = value;
                serializedObject.Update();
            }
        }

        bool haveMultipleTypeValue
        {
            get
            {
                var objects = serializedObject.targetObjects;
                LightType value = (objects[0] as AdditionalLightData).type;
                for (int index = 1; index < objects.Length; ++index)
                    if (value != (objects[index] as AdditionalLightData).type)
                        return true;
                return false;
            }
        }

        public SerializedHDLight(AdditionalLightData[] lightDatas, LightEditor.Settings settings)
        {
            serializedObject = new SerializedObject(lightDatas);
            this.settings = settings;
            
            // Include the UniversalAdditionalLightData properties
            lightsAdditionalData = CoreEditorUtils.GetAdditionalData<UniversalAdditionalLightData>(serializedObject.targetObjects);
            serializedAdditionalDataObject = new SerializedObject(lightsAdditionalData);
            
            useAdditionalDataProp = serializedAdditionalDataObject.FindProperty("m_UsePipelineSettings");
            additionalLightsShadowResolutionTierProp = serializedAdditionalDataObject.FindProperty("m_AdditionalLightsShadowResolutionTier");
            softShadowQualityProp = serializedAdditionalDataObject.FindProperty("m_SoftShadowQuality");
            lightCookieSizeProp = serializedAdditionalDataObject.FindProperty("m_LightCookieSize");
            lightCookieOffsetProp = serializedAdditionalDataObject.FindProperty("m_LightCookieOffset");

            renderingLayers = serializedAdditionalDataObject.FindProperty("m_RenderingLayers");
            customShadowLayers = serializedAdditionalDataObject.FindProperty("m_CustomShadowLayers");
            shadowRenderingLayers = serializedAdditionalDataObject.FindProperty("m_ShadowRenderingLayers");

            settings.ApplyModifiedProperties(); // end of the UniversalAdditionalLightData 
            
            using (var o = new PropertyFetcher<AdditionalLightData>(serializedObject))
            {
                intensity = o.Find("m_Intensity");
                enableSpotReflector = o.Find("m_EnableSpotReflector");
                luxAtDistance = o.Find("m_LuxAtDistance");

                volumetricDimmer = o.Find("m_VolumetricDimmer");
                volumetricFadeDistance = o.Find("m_VolumetricFadeDistance");
                lightUnit = o.Find("m_LightUnit");
                
                aspectRatio = o.Find("m_AspectRatio");

                interactsWithSky = o.Find("m_InteractsWithSky");
                angularDiameter = o.Find("m_AngularDiameter");
                flareSize = o.Find("m_FlareSize");
                flareFalloff = o.Find("m_FlareFalloff");
                flareTint = o.Find("m_FlareTint");
                surfaceTexture = o.Find("m_SurfaceTexture");
                surfaceTint = o.Find("m_SurfaceTint");
                distance = o.Find("m_Distance");

                useVolumetric = o.Find("useVolumetric");

                // Shadow datas:
                shadowDimmer = o.Find("m_ShadowDimmer");
                volumetricShadowDimmer = o.Find("m_VolumetricShadowDimmer");
                shadowFadeDistance = o.Find("m_ShadowFadeDistance");
            }

            lightGameObject = new SerializedObject(serializedObject.targetObjects.Select(ld => ((AdditionalLightData)ld).gameObject).ToArray());
        }

        public void Update()
        {
            // Case 1182968
            // For some reasons, the is different cache is not updated while we actually have different
            // values for shadowResolution.level
            // So we force the update here as a workaround
            serializedObject.SetIsDifferentCacheDirty();

            serializedObject.Update();
            settings.Update();

            lightGameObject.Update();

            serializedAdditionalDataObject.Update(); // URP
        }

        public void Apply()
        {
            serializedObject.ApplyModifiedProperties();
            settings.ApplyModifiedProperties();

            serializedAdditionalDataObject.ApplyModifiedProperties(); // URP
        }
    }
}
