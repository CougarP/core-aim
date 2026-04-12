namespace Core_Aim.Views.Splash
{
    /// <summary>
    /// Maps a persisted SplashStyle string into the matching ISplashWindow.
    /// Defaults to NeuralBoot when the style is empty/unknown.
    /// </summary>
    public static class SplashFactory
    {
        public static ISplashWindow Create(string? style)
        {
            return style switch
            {
                SplashStyles.NeuralBoot          => new SplashNeuralBootWindow(),
                SplashStyles.ReactorIgnition     => new SplashReactorWindow(),
                SplashStyles.HolographicAssembly => new SplashHoloWindow(),
                _                                 => new SplashNeuralBootWindow(),
            };
        }
    }
}
