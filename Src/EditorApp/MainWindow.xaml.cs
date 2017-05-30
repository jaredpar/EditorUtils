using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using EditorUtils;

namespace EditorApp
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            var editorHostFactory = new EditorHostFactory(EditorVersion.Vs2017);
            CreateContent(editorHostFactory);
        }

        private void CreateContent(EditorHostFactory editorHostFactory)
        {
            var editorHost = editorHostFactory.CreateEditorHost();

            var textBuffer = editorHost.TextBufferFactoryService.CreateTextBuffer();
            textBuffer.Insert(0, "Hello Editor");

            var wpfTextView = editorHost.TextEditorFactoryService.CreateTextView(textBuffer);
            var wpfTextViewHost = editorHost.TextEditorFactoryService.CreateTextViewHost(wpfTextView, setFocus: true);
            Content = wpfTextViewHost.HostControl;
        }
    }
}
