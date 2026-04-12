using System;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;

namespace Core_Aim.Services
{
    /// <summary>
    /// XAML markup extension for localized strings.
    /// Usage: Text="{loc:Loc Hardware}"
    /// Equivalent to: Text="{Binding [Hardware], Source={x:Static loc:LocalizationManager.Instance}}"
    /// </summary>
    [MarkupExtensionReturnType(typeof(object))]
    public class LocExtension : MarkupExtension
    {
        public string Key { get; }

        public LocExtension(string key)
        {
            Key = key;
        }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            var binding = new System.Windows.Data.Binding($"[{Key}]")
            {
                Source = LocalizationManager.Instance,
                Mode   = System.Windows.Data.BindingMode.OneWay,
            };
            return binding.ProvideValue(serviceProvider);
        }
    }
}
