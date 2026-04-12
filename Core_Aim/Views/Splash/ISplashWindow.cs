using System;

namespace Core_Aim.Views.Splash
{
    /// <summary>
    /// Phantom splash style identifiers — also persisted in AppConfig.SplashStyle.
    /// </summary>
    public static class SplashStyles
    {
        public const string NeuralBoot          = "neural";
        public const string ReactorIgnition     = "reactor";
        public const string HolographicAssembly = "holo";
    }

    /// <summary>
    /// Common contract every Phantom splash window implements so App.xaml.cs
    /// can swap implementations without caring which concept is active.
    /// </summary>
    public interface ISplashWindow
    {
        event Action? Completed;
        void Show();
        void Close();
    }
}
