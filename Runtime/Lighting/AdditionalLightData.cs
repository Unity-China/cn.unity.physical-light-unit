using System;
using System.Collections.Generic;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.Serialization;

namespace UnityEngine.Rendering.Universal
{
    // This structure contains all the old values for every recordable fields from the HD light editor
    // so we can force timeline to record changes on other fields from the LateUpdate function (editor only)
    struct TimelineWorkaround
    {
        public float oldSpotAngle;
        public Color oldLightColor;
        public Vector3 oldLossyScale;
        // public bool oldDisplayAreaLightEmissiveMesh;
        public float oldLightColorTemperature;
        public float oldIntensity;
        public bool lightEnabled;
    }

    /// <summary>
    /// Class containing various additional Physical light Unit data used by URP.
    /// </summary>
    [HDRPHelpURLAttribute("Light-Component")]
    [AddComponentMenu("")] // Hide in menu
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Light))]
    [ExecuteAlways]
    public partial class AdditionalLightData : MonoBehaviour, ISerializationCallbackReceiver, IAdditionalData
    {
        Exposure exposureVolumeComponent;
        Volume volume;
        float exposureValue = 1f;

        /// <summary>
        /// The default intensity value for directional lights in Lux
        /// </summary>
        public const float k_DefaultDirectionalLightIntensity = Mathf.PI; // In lux
        /// <summary>
        /// The default intensity value for punctual lights in Lumen
        /// </summary>
        public const float k_DefaultPunctualLightIntensity = 600.0f;      // Light default to 600 lumen, i.e ~48 candela
        /// <summary>
        /// The default intensity value for area lights in Lumen
        /// </summary>
        public const float k_DefaultAreaLightIntensity = 200.0f;          // Light default to 200 lumen to better match point light

        /// <summary>
        /// Minimum aspect ratio for pyramid spot lights
        /// </summary>
        public const float k_MinAspectRatio = 0.05f;
        /// <summary>
        /// Maximum aspect ratio for pyramid spot lights
        /// </summary>
        public const float k_MaxAspectRatio = 20.0f;

        /// <summary>List of the lights that overlaps when the OverlapLight scene view mode is enabled</summary>
        internal static HashSet<AdditionalLightData> s_overlappingHDLights = new HashSet<AdditionalLightData>();

        #region HDLight Properties API
        [SerializeField, FormerlySerializedAs("displayLightIntensity")]
        float m_Intensity;
        /// <summary>
        /// Get/Set the intensity of the light using the current light unit.
        /// </summary>
        public float intensity
        {
            get => m_Intensity;
            set
            {
                if (m_Intensity == value)
                    return;

                m_Intensity = Mathf.Clamp(value, 0, float.MaxValue);
                UpdateLightIntensity();
            }
        }

        // Only for Spotlight, should be hide for other light
        [SerializeField, FormerlySerializedAs("enableSpotReflector")]
        bool m_EnableSpotReflector = true;
        /// <summary>
        /// Get/Set the Spot Reflection option on spot lights.
        /// </summary>
        public bool enableSpotReflector
        {
            get => m_EnableSpotReflector;
            set
            {
                if (m_EnableSpotReflector == value)
                    return;

                m_EnableSpotReflector = value;
                UpdateLightIntensity();
            }
        }

        // Lux unity for all light except directional require a distance
        [SerializeField, FormerlySerializedAs("luxAtDistance")]
        float m_LuxAtDistance = 1.0f;
        /// <summary>
        /// Set/Get the distance for spot lights where the emission intensity is matches the value set in the intensity property.
        /// </summary>
        public float luxAtDistance
        {
            get => m_LuxAtDistance;
            set
            {
                if (m_LuxAtDistance == value)
                    return;

                m_LuxAtDistance = Mathf.Clamp(value, 0, float.MaxValue);
                UpdateLightIntensity();
            }
        }

        [Range(0.0f, 16.0f), SerializeField, FormerlySerializedAs("volumetricDimmer")]
        float m_VolumetricDimmer = 1.0f;
        /// <summary>
        /// Get/Set the light dimmer / multiplier on volumetric effects, between 0 and 16.
        /// </summary>
        public float volumetricDimmer
        {
            get => useVolumetric ? m_VolumetricDimmer : 0.0f;
            set
            {
                if (m_VolumetricDimmer == value)
                    return;

                m_VolumetricDimmer = Mathf.Clamp(value, 0.0f, 16.0f);

                // if (lightEntity.valid)
                //     HDLightRenderDatabase.instance.EditLightDataAsRef(lightEntity).volumetricDimmer = m_VolumetricDimmer;
            }
        }
        
        // Used internally to convert any light unit input into light intensity
        [SerializeField, FormerlySerializedAs("lightUnit")]
        LightUnit m_LightUnit = LightUnit.Lumen;
        /// <summary>
        /// Get/Set the light unit. When changing the light unit, the intensity will be converted to match the previous intensity in the new unit.
        /// </summary>
        public LightUnit lightUnit
        {
            get => m_LightUnit;
            set
            {
                if (m_LightUnit == value)
                    return;

                if (!IsValidLightUnitForType(legacyLight.type, value))
                {
                    var supportedTypes = String.Join(", ", GetSupportedLightUnits(legacyLight.type));
                    Debug.LogError($"Set Light Unit '{value}' to a {GetLightTypeName()} is not allowed, only {supportedTypes} are supported.");
                    return;
                }

                LightUtils.ConvertLightIntensity(m_LightUnit, value, this, legacyLight);

                m_LightUnit = value;
                UpdateLightIntensity();
            }
        }

        // Not used for directional lights.
        [SerializeField, FormerlySerializedAs("fadeDistance")]
        float m_FadeDistance = 10000.0f;
        /// <summary>
        /// Get/Set the light fade distance.
        /// </summary>
        public float fadeDistance
        {
            get => m_FadeDistance;
            set
            {
                if (m_FadeDistance == value)
                    return;

                m_FadeDistance = Mathf.Clamp(value, 0, float.MaxValue);

                // if (lightEntity.valid)
                //     HDLightRenderDatabase.instance.EditLightDataAsRef(lightEntity).fadeDistance = m_FadeDistance;
            }
        }

        // Not used for directional lights.
        [SerializeField]
        float m_VolumetricFadeDistance = 10000.0f;
        /// <summary>
        /// Get/Set the light fade distance for volumetrics.
        /// </summary>
        public float volumetricFadeDistance
        {
            get => m_VolumetricFadeDistance;
            set
            {
                if (m_VolumetricFadeDistance == value)
                    return;

                m_VolumetricFadeDistance = Mathf.Clamp(value, 0, float.MaxValue);
                // if (lightEntity.valid)
                //     HDLightRenderDatabase.instance.EditLightDataAsRef(lightEntity).volumetricFadeDistance = m_VolumetricFadeDistance;
            }
        }

        // Only for pyramid projector
        [SerializeField, FormerlySerializedAs("aspectRatio")]
        float m_AspectRatio = 1.0f;
        /// <summary>
        /// Get/Set the aspect ratio of a pyramid light
        /// </summary>
        public float aspectRatio
        {
            get => m_AspectRatio;
            set
            {
                if (m_AspectRatio == value)
                    return;

                m_AspectRatio = Mathf.Clamp(value, k_MinAspectRatio, k_MaxAspectRatio);
                UpdateAllLightValues();
            }
        }
        
        // TODO: Celestial Body UI in Directional Light only For Physical Sky

        // Directional lights only.
        [SerializeField, FormerlySerializedAs("interactsWithSky")]
        bool m_InteractsWithSky = true;
        /// <summary>
        /// Controls if the directional light affect the Physically Based sky.
        /// This have no effect on other skies.
        /// </summary>
        public bool interactsWithSky
        {
            // m_InteractWithSky can be true if user changed from directional to point light, so we need to check current type
            get => m_InteractsWithSky && legacyLight.type == LightType.Directional; 
            set
            {
                if (m_InteractsWithSky == value)
                    return;

                m_InteractsWithSky = value;
                // if (lightEntity.valid)
                //     HDLightRenderDatabase.instance.EditLightDataAsRef(lightEntity).interactsWithSky = m_InteractsWithSky;
            }
        }
        [SerializeField, FormerlySerializedAs("angularDiameter")]
        float m_AngularDiameter = 0.5f;
        /// <summary>
        /// Angular diameter of the emissive celestial body represented by the light as seen from the camera (in degrees).
        /// Used to render the sun/moon disk.
        /// </summary>
        public float angularDiameter
        {
            get => m_AngularDiameter;
            set
            {
                if (m_AngularDiameter == value)
                    return;

                m_AngularDiameter = value; // Serialization code clamps
                // if (lightEntity.valid)
                //     HDLightRenderDatabase.instance.EditLightDataAsRef(lightEntity).angularDiameter = m_AngularDiameter;
            }
        }

        [SerializeField, FormerlySerializedAs("flareSize")]
        float m_FlareSize = 2.0f;
        /// <summary>
        /// Size the flare around the celestial body (in degrees).
        /// </summary>
        public float flareSize
        {
            get => m_FlareSize;
            set
            {
                if (m_FlareSize == value)
                    return;

                m_FlareSize = value; // Serialization code clamps
                // if (lightEntity.valid)
                //     HDLightRenderDatabase.instance.EditLightDataAsRef(lightEntity).flareSize = m_FlareSize;
            }
        }

        [SerializeField, FormerlySerializedAs("flareTint")]
        Color m_FlareTint = Color.white;
        /// <summary>
        /// Tints the flare of the celestial body.
        /// </summary>
        public Color flareTint
        {
            get => m_FlareTint;
            set
            {
                if (m_FlareTint == value)
                    return;

                m_FlareTint = value;
                // if (lightEntity.valid)
                //     HDLightRenderDatabase.instance.EditLightDataAsRef(lightEntity).flareTint = m_FlareTint;
            }
        }

        [SerializeField, FormerlySerializedAs("flareFalloff")]
        float m_FlareFalloff = 4.0f;
        /// <summary>
        /// The falloff rate of flare intensity as the angle from the light increases.
        /// </summary>
        public float flareFalloff
        {
            get => m_FlareFalloff;
            set
            {
                if (m_FlareFalloff == value)
                    return;

                m_FlareFalloff = value; // Serialization code clamps
                // if (lightEntity.valid)
                //     HDLightRenderDatabase.instance.EditLightDataAsRef(lightEntity).flareFalloff = m_FlareFalloff;
            }
        }

        [SerializeField, FormerlySerializedAs("surfaceTexture")]
        Texture2D m_SurfaceTexture = null;
        /// <summary>
        /// 2D (disk) texture of the surface of the celestial body. Acts like a multiplier.
        /// </summary>
        public Texture2D surfaceTexture
        {
            get => m_SurfaceTexture;
            set
            {
                if (m_SurfaceTexture == value)
                    return;

                m_SurfaceTexture = value;
            }
        }

        [SerializeField, FormerlySerializedAs("surfaceTint")]
        Color m_SurfaceTint = Color.white;
        /// <summary>
        /// Tints the surface of the celestial body.
        /// </summary>
        public Color surfaceTint
        {
            get => m_SurfaceTint;
            set
            {
                if (m_SurfaceTint == value)
                    return;

                m_SurfaceTint = value;
                // if (lightEntity.valid)
                //     HDLightRenderDatabase.instance.EditLightDataAsRef(lightEntity).surfaceTint = m_SurfaceTint;
            }
        }

        [SerializeField, FormerlySerializedAs("distance")]
        float m_Distance = 150000000000; // Sun to Earth
        /// <summary>
        /// Distance from the camera to the emissive celestial body represented by the light.
        /// </summary>
        public float distance
        {
            get => m_Distance;
            set
            {
                if (m_Distance == value)
                    return;

                m_Distance = value; // Serialization code clamps
                // if (lightEntity.valid)
                //     HDLightRenderDatabase.instance.EditLightDataAsRef(lightEntity).distance = m_Distance;
            }
        }
        
        /// <summary>
        /// Color of the light.
        /// </summary>
        public Color color
        {
            get => legacyLight.color;
            set => legacyLight.color = value;
        }
        #endregion // HDLight Properties API
        
        #region HDShadow Properties API (from AdditionalShadowData)

        [Range(0.0f, 1.0f)]
        [SerializeField]
        float m_VolumetricShadowDimmer = 1.0f;
        /// <summary>
        /// Get/Set the volumetric shadow dimmer value, between 0 and 1.
        /// </summary>
        public float volumetricShadowDimmer
        {
            get => useVolumetric ? m_VolumetricShadowDimmer : 0.0f;
            set
            {
                if (m_VolumetricShadowDimmer == value)
                    return;

                m_VolumetricShadowDimmer = Mathf.Clamp01(value);
            }
        }

        [SerializeField]
        float m_ShadowFadeDistance = 10000.0f;
        /// <summary>
        /// Get/Set the shadow fade distance.
        /// </summary>
        public float shadowFadeDistance
        {
            get => m_ShadowFadeDistance;
            set
            {
                if (m_ShadowFadeDistance == value)
                    return;

                m_ShadowFadeDistance = Mathf.Clamp(value, 0, float.MaxValue);
            }
        }
        #endregion // HDShadow Properties API (from AdditionalShadowData)

#pragma warning disable 0414 // The field '...' is assigned but its value is never used, these fields are used by the inspector
        [SerializeField, FormerlySerializedAs("useVolumetric")]
        bool useVolumetric = true;
#pragma warning restore 0414

        // Runtime datas used to compute light intensity
        [NonSerialized]
        Light m_Light;
        internal Light legacyLight
        {
            get
            {
                // Calling TryGetComponent only when needed is faster than letting the null check happen inside TryGetComponent
                if (m_Light == null)
                    TryGetComponent<Light>(out m_Light);

                return m_Light;
            }
        }

        void OnDisable()
        {
            s_overlappingHDLights.Remove(this);
        }


        // We need these old states to make timeline and the animator record the intensity value and the emissive mesh changes
        [System.NonSerialized]
        TimelineWorkaround timelineWorkaround = new TimelineWorkaround();


        internal bool useColorTemperature
        {
            get => legacyLight.useColorTemperature;
            set
            {
                if (legacyLight.useColorTemperature == value)
                    return;

                legacyLight.useColorTemperature = value;
            }
        }

        // TODO: we might be able to get rid to that
        [System.NonSerialized]
        bool m_Animated;

        private void Start()
        {
            // If there is an animator attached ot the light, we assume that some of the light properties
            // might be driven by this animator (using timeline or animations) so we force the LateUpdate
            // to sync the animated HDAdditionalLightData properties with the light component.
            m_Animated = GetComponent<Animator>() != null;
        }

        // TODO: There are a lot of old != current checks and assignation in this function, maybe think about using another system ?
        void LateUpdate()
        {
            // Prevent any unwanted sync when not in HDRP (case 1217575)
            if (RenderPipelineManager.currentPipeline == null)
                return;

            // We force the animation in the editor and in play mode when there is an animator component attached to the light
#if !UNITY_EDITOR
            if (!m_Animated)
                return;
#endif

#if UNITY_EDITOR
            
            // Update the list of overlapping lights for the LightOverlap scene view mode
            if (IsOverlapping())
                s_overlappingHDLights.Add(this);
            else
                s_overlappingHDLights.Remove(this);
#endif

            if (legacyLight.enabled != timelineWorkaround.lightEnabled)
            {

                timelineWorkaround.lightEnabled = legacyLight.enabled;
            }
            
            // Check if the intensity have been changed by the inspector or an animator
            if (timelineWorkaround.oldLossyScale != transform.lossyScale
                || intensity != timelineWorkaround.oldIntensity
                || legacyLight.colorTemperature != timelineWorkaround.oldLightColorTemperature)
            {
                UpdateLightIntensity();

                timelineWorkaround.oldLossyScale = transform.lossyScale;
                timelineWorkaround.oldIntensity = intensity;
                timelineWorkaround.oldLightColorTemperature = legacyLight.colorTemperature;
            }

            // Same check for light angle to update intensity using spot angle
            if (legacyLight.type == LightType.Spot && (timelineWorkaround.oldSpotAngle != legacyLight.spotAngle))
            {
                UpdateLightIntensity();
                timelineWorkaround.oldSpotAngle = legacyLight.spotAngle;
            }

            if (legacyLight.color != timelineWorkaround.oldLightColor
                || timelineWorkaround.oldLossyScale != transform.lossyScale
                || legacyLight.colorTemperature != timelineWorkaround.oldLightColorTemperature)
            {
                timelineWorkaround.oldLightColor = legacyLight.color;
                timelineWorkaround.oldLossyScale = transform.lossyScale;
                timelineWorkaround.oldLightColorTemperature = legacyLight.colorTemperature;
            }
        }

        void OnDidApplyAnimationProperties()
        {
            UpdateAllLightValues();
        }

        // As we have our own default value, we need to initialize the light intensity correctly
        /// <summary>
        /// Initialize an AdditionalLightData that have just beeing created.
        /// </summary>
        /// <param name="lightData"></param>
        public static void InitDefaultHDAdditionalLightData(AdditionalLightData lightData)
        {
            // Special treatment for Unity built-in area light. Change it to our rectangle light
            var light = lightData.gameObject.GetComponent<Light>();

            // Set light intensity and unit using its type
            //note: requiring type convert Rectangle and Disc to Area and correctly set areaLight
            switch (lightData.legacyLight.type)
            {
                case LightType.Directional:
                    lightData.lightUnit = LightUnit.Lux;
                    lightData.intensity = k_DefaultDirectionalLightIntensity / Mathf.PI * 100000.0f; // Change back to just k_DefaultDirectionalLightIntensity on 11.0.0 (can't change constant as it's a breaking change)
                    break;
                case LightType.Area: // Rectangle by default when light is created
                    switch (lightData.legacyLight.type)
                    {
                        case LightType.Rectangle:
                            lightData.lightUnit = LightUnit.Lumen;
                            lightData.intensity = k_DefaultAreaLightIntensity;
                            light.shadowNearPlane = 0;
                            light.shadows = LightShadows.None;
#if UNITY_EDITOR
                            light.areaSize = new Vector2(0.5f, 0.5f);
#endif
                            break;
                        case LightType.Disc:
                            //[TODO: to be defined]
                            break;
                    }
                    break;
                case LightType.Point:
                case LightType.Spot:
                    lightData.lightUnit = LightUnit.Lumen;
                    lightData.intensity = k_DefaultPunctualLightIntensity;
                    break;
            }

            // We don't use the global settings of shadow mask by default
            light.lightShadowCasterMode = LightShadowCasterMode.Everything;

            // lightData.normalBias = 0.75f;            light.shadowNormalBias = 0.75f;
            // lightData.slopeBias = 0.5f;

            // Enable filter/temperature mode by default for all light types
            lightData.useColorTemperature = true;
        }


        #region Update functions to patch values in the Light component when we change properties inside HDAdditionalLightData

        void SetLightIntensityPunctual(float intensity)
        {
            switch (legacyLight.type)
            {
                case LightType.Directional:
                    legacyLight.intensity = intensity; // Always in lux
                    break;
                case LightType.Point:
                    if (lightUnit == LightUnit.Candela)
                        legacyLight.intensity = intensity;
                    else
                        legacyLight.intensity = LightUtils.ConvertPointLightLumenToCandela(intensity);
                    break;
                case LightType.Spot:
                    if (lightUnit == LightUnit.Candela)
                    {
                        // When using candela, reflector don't have any effect. Our intensity is candela = lumens/steradian and the user
                        // provide desired value for an angle of 1 steradian.
                        legacyLight.intensity = intensity;
                    }
                    else  // lumen
                    {
                        if (enableSpotReflector)
                        {
                            // If reflector is enabled all the lighting from the sphere is focus inside the solid angle of current shape
                            // if (spotLightShape == SpotLightShape.Cone)
                            // {
                                legacyLight.intensity = LightUtils.ConvertSpotLightLumenToCandela(intensity, legacyLight.spotAngle * Mathf.Deg2Rad, true);
                            // }
                            // else if (spotLightShape == SpotLightShape.Pyramid)
                            // {
                            //     float angleA, angleB;
                            //     LightUtils.CalculateAnglesForPyramid(aspectRatio, legacyLight.spotAngle * Mathf.Deg2Rad, out angleA, out angleB);
                            //
                            //     legacyLight.intensity = LightUtils.ConvertFrustrumLightLumenToCandela(intensity, angleA, angleB);
                            // }
                            // else // Box shape, fallback to punctual light.
                            // {
                            //     legacyLight.intensity = LightUtils.ConvertPointLightLumenToCandela(intensity);
                            // }
                        }
                        else
                        {
                            // No reflector, angle act as occlusion of point light.
                            legacyLight.intensity = LightUtils.ConvertPointLightLumenToCandela(intensity);
                        }
                    }
                    break;
            }
        }

        void UpdateLightIntensity()
        {
            if (lightUnit == LightUnit.Lumen)
            {
                // if (m_PointlightHDType == PointLightHDType.Punctual)
                if (legacyLight.type != LightType.Area )
                    SetLightIntensityPunctual(intensity);
#if UNITY_EDITOR
                // Area Light (Editor Only for URP)
                else
                    legacyLight.intensity = LightUtils.ConvertAreaLightLumenToLuminance(legacyLight.type, intensity, legacyLight.areaSize.x, legacyLight.areaSize.y);
#endif
            }
            else if (lightUnit == LightUnit.Ev100)
            {
                legacyLight.intensity = LightUtils.ConvertEvToLuminance(m_Intensity);
            }
            else
            {
                LightType lightType = legacyLight.type;
                if ((lightType == LightType.Spot || lightType == LightType.Point) && lightUnit == LightUnit.Lux)
                {
                    // Box are local directional light with lux unity without at distance
                    // if ((lightType == LightType.Spot) && (spotLightShape == SpotLightShape.Box))
                    //     legacyLight.intensity = m_Intensity;
                    // else
                        legacyLight.intensity = LightUtils.ConvertLuxToCandela(m_Intensity, luxAtDistance);
                }
                else
                    legacyLight.intensity = m_Intensity;
            }
        
            // add exposure
            legacyLight.intensity *= exposureValue;

#if UNITY_EDITOR
            legacyLight.SetLightDirty(); // Should be apply only to parameter that's affect GI, but make the code cleaner
#endif
        }

        /// <summary>
        /// Synchronize all the HD Additional Light values with the Light component.
        /// </summary>
        public void UpdateAllLightValues()
        {
            // Update light intensity
            UpdateLightIntensity();
        }

        #endregion

        #region User API functions

        /// <summary>
        /// Set the color of the light.
        /// </summary>
        /// <param name="color">Color</param>
        /// <param name="colorTemperature">Optional color temperature</param>
        public void SetColor(Color color, float colorTemperature = -1)
        {
            if (colorTemperature != -1)
            {
                legacyLight.colorTemperature = colorTemperature;
                useColorTemperature = true;
            }

            this.color = color;
        }

        /// <summary>
        /// Toggle the usage of color temperature.
        /// </summary>
        /// <param name="enable"></param>
        public void EnableColorTemperature(bool enable)
        {
            useColorTemperature = enable;
        }

        /// <summary>
        /// Set the intensity of the light using the current unit.
        /// </summary>
        /// <param name="intensity"></param>
        public void SetIntensity(float intensity) => this.intensity = intensity;

        /// <summary>
        /// Set the intensity of the light using unit in parameter.
        /// </summary>
        /// <param name="intensity"></param>
        /// <param name="unit">Unit must be a valid Light Unit for the current light type</param>
        public void SetIntensity(float intensity, LightUnit unit)
        {
            this.lightUnit = unit;
            this.intensity = intensity;
        }

        /// <summary>
        /// For Spot Lights only, set the intensity that the spot should emit at a certain distance in meter
        /// </summary>
        /// <param name="luxIntensity"></param>
        /// <param name="distance"></param>
        public void SetSpotLightLuxAt(float luxIntensity, float distance)
        {
            lightUnit = LightUnit.Lux;
            luxAtDistance = distance;
            intensity = luxIntensity;
        }

        /// <summary>
        /// Set the light unit.
        /// </summary>
        /// <param name="unit">Unit of the light</param>
        public void SetLightUnit(LightUnit unit) => lightUnit = unit;

        /// <summary>
        /// Get the list of supported light units depending on the current light type.
        /// </summary>
        /// <returns></returns>
        public LightUnit[] GetSupportedLightUnits() => GetSupportedLightUnits(legacyLight.type);

        #endregion // User API Functions

        /// <summary>
        /// Deserialization callback
        /// </summary>
        void ISerializationCallbackReceiver.OnAfterDeserialize(){}
        
        /// <summary>
        /// Serialization callback
        /// </summary>
        void ISerializationCallbackReceiver.OnBeforeSerialize(){}

        internal void Reset()
            => InitDefaultHDAdditionalLightData(this);

        void Update()
        {
    #region Exposure
            if (exposureVolumeComponent == null)
            {
                volume = FindObjectOfType<Volume>() as Volume;
                if (volume != null && volume.sharedProfile != null)
                {
                    volume.sharedProfile.TryGet<Exposure>(out exposureVolumeComponent);
                }
            }

            if(exposureVolumeComponent != null && exposureVolumeComponent.IsActive())
            {
                float newExposure = exposureVolumeComponent.exposure;
                if ( Mathf.Abs(exposureValue - newExposure) > Mathf.Epsilon )
                {
                    exposureValue = newExposure;
                    UpdateLightIntensity();
                }
            }
            else
            {
                float newExposure = 1f;
                if (Mathf.Abs(exposureValue - newExposure) > Mathf.Epsilon)
                {
                    exposureValue = newExposure;
                    UpdateLightIntensity();
                }
            }
    #endregion
        }

        /// <summary>Tell if the light is overlapping for the light overlap debug mode</summary>
        internal bool IsOverlapping()
        {
            var baking = GetComponent<Light>().bakingOutput;
            bool isOcclusionSeparatelyBaked = baking.occlusionMaskChannel != -1;
            bool isDirectUsingBakedOcclusion = baking.mixedLightingMode == MixedLightingMode.Shadowmask || baking.mixedLightingMode == MixedLightingMode.Subtractive;
            return isDirectUsingBakedOcclusion && !isOcclusionSeparatelyBaked;
        }
    }
}
