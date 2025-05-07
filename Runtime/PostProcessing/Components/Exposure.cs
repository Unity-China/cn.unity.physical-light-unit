using System;
using UnityEngine.Rendering.HighDefinition;

namespace UnityEngine.Rendering.Universal
{
    /// <summary>
    /// A volume component that holds settings for the Exposure effect.
    /// </summary>
    [Serializable, VolumeComponentMenuForRenderPipeline("Exposure", typeof(UniversalRenderPipeline))]
    [HDRPHelpURLAttribute("Override-Exposure")]
    public sealed class Exposure : VolumeComponent, IPostProcessComponent
    {
        /// <summary>
        /// Specifies the method that HDRP uses to process exposure.
        /// </summary>
        /// <seealso cref="ExposureMode"/>
        [Tooltip("Specifies the method that HDRP uses to process exposure.")]
        public ExposureModeParameter mode = new ExposureModeParameter(ExposureMode.Fixed);
        
        /// <summary>
        /// Sets a static exposure value for Cameras in this Volume.
        /// This parameter is only used when <see cref="ExposureMode.Fixed"/> is set.
        /// </summary>
        [Tooltip("Sets a static exposure value for Cameras in this Volume.")]
        public FloatParameter fixedExposure = new FloatParameter(0f);
        /// <summary>
        /// Sets the compensation that the Camera applies to the calculated exposure value.
        /// This parameter is only used when any mode but <see cref="ExposureMode.Fixed"/> is set.
        /// </summary>
        [Tooltip("Sets the compensation that the Camera applies to the calculated exposure value.")]
        public FloatParameter compensation = new FloatParameter(0f);

        
        
        public float exposure
        {
            get => ColorUtils.ConvertEV100ToExposure(fixedExposure.value);
        }

        public void SetSkyboxExposure(Material skybox, float ev100, float skyEv)
        {
            if (skybox == null) return;
        
            float skyIntensity = ColorUtils.ConvertEV100ToExposure(ev100) * ColorUtils.ConvertEV100ToExposure(-skyEv);
            skybox.SetFloat("_Exposure", skyIntensity);
        }

        /// <summary>
        /// Tells if the effect needs to be rendered or not.
        /// </summary>
        /// <returns><c>true</c> if the effect should be rendered, <c>false</c> otherwise.</returns>
        public bool IsActive()
        {
            return true;
        }
        
        public bool IsTileCompatible() => true;
    }
    
    /// <summary>
    /// Methods that HDRP uses to process exposure.
    /// </summary>
    /// <seealso cref="Exposure.mode"/>
    public enum ExposureMode
    {
        /// <summary>
        /// Allows you to manually sets the Scene exposure.
        /// </summary>
        Fixed = 0,

        /// <summary>
        /// Automatically sets the exposure depending on what is on screen.
        /// </summary>
        // Automatic = 1,

        /// <summary>
        /// Automatically sets the exposure depending on what is on screen and can filter out outliers based on provided settings.
        /// </summary>
        // AutomaticHistogram = 4,

        /// <summary>
        /// Maps the current Scene exposure to a custom curve.
        /// </summary>
        // CurveMapping = 2,

        /// <summary>
        /// Uses the current physical Camera settings to set the Scene exposure.
        /// </summary>
        [InspectorName("Physical Camera")]
        UsePhysicalCamera = 3
    }
    
    
    /// <summary>
    /// A <see cref="VolumeParameter"/> that holds a <see cref="ExposureMode"/> value.
    /// </summary>
    [Serializable]
    public sealed class ExposureModeParameter : VolumeParameter<ExposureMode>
    {
        /// <summary>
        /// Creates a new <see cref="ExposureModeParameter"/> instance.
        /// </summary>
        /// <param name="value">The initial value to store in the parameter.</param>
        /// <param name="overrideState">The initial override state for the parameter.</param>
        public ExposureModeParameter(ExposureMode value, bool overrideState = false) : base(value, overrideState) { }
    }
}
