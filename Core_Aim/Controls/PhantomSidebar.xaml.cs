using System.Windows.Controls;

namespace Core_Aim.Controls
{
    /// <summary>
    /// Phantom Sidebar — 38px vertical icon rail.
    /// All wiring is via XAML bindings: each item binds IsChecked → IsXXVisible (OneWay)
    /// and Command → ToggleTabCommand (with the tab code as CommandParameter).
    /// The host VM (MainViewModel) owns the actual tab state.
    /// </summary>
    public partial class PhantomSidebar : System.Windows.Controls.UserControl
    {
        public PhantomSidebar()
        {
            InitializeComponent();
        }
    }
}
