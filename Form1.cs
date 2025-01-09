using System;
using System.IO;
using System.Windows.Forms;
using Microsoft.Web.WebView2.WinForms;

namespace TraffiCCam
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();

            WebView2 webView = new WebView2();
            webView.Dock = DockStyle.Fill;
            this.Controls.Add(webView);

            // Inicializa el WebView2
            webView.CoreWebView2InitializationCompleted += (sender, e) =>
            {
                // Obtiene la ruta completa del archivo HTML dentro del proyecto
                string appDirectory = AppDomain.CurrentDomain.BaseDirectory;
                string htmlFilePath = Path.Combine(appDirectory, "html", "index.html");

                // Verifica si el archivo existe antes de navegar
                if (File.Exists(htmlFilePath))
                {
                    webView.CoreWebView2.Navigate(new Uri(htmlFilePath).AbsoluteUri);
                }
                else
                {
                    MessageBox.Show("El archivo HTML no se encontró: " + htmlFilePath);
                }
            };
            webView.EnsureCoreWebView2Async();
        }
    }
}