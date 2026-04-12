using System.Windows;
// CRIAMOS UM APELIDO (ALIAS) PARA EVITAR CONFLITOS
using Media = System.Windows.Media;

namespace Core_Aim.Views
{
    public partial class ModernColorPickerWindow : Window
    {
        // Usamos 'Media.Color' explicitamente
        public Media.Color SelectedColor { get; private set; } = Media.Colors.White;

        public ModernColorPickerWindow(Media.Color initialColor)
        {
            InitializeComponent();
            // Define a cor inicial do Canvas
            MyColorCanvas.SelectedColor = initialColor;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            // Se for nulo, usa preto (Media.Colors.Black) como fallback
            SelectedColor = MyColorCanvas.SelectedColor ?? Media.Colors.Black;

            this.DialogResult = true; // Fecha a janela com sucesso
        }
    }
}