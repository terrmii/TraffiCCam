using System;
using System.IO;
using System.Windows.Forms;
using Microsoft.Web.WebView2.WinForms;

namespace TraffiCCam
{
    public partial class Form1 : Form
    {
        private WebView2 webView;
        private bool isWebViewReady = false;

        public Form1()
        {
            InitializeComponent();

            webView = new WebView2();
            webView.Dock = DockStyle.Fill;
            this.Controls.Add(webView);

            // Inicializa el WebView2 y maneja eventos
            InitializeWebView();
        }

        private async void InitializeWebView()
        {
            try
            {
                // Asegura la inicialización del WebView2
                await webView.EnsureCoreWebView2Async();

                // Configura el estado de inicialización
                isWebViewReady = true;

                string appDirectory = AppDomain.CurrentDomain.BaseDirectory;
                string htmlFilePath = Path.Combine(appDirectory, "html", "index.html");

                if (File.Exists(htmlFilePath))
                {
                    webView.CoreWebView2.Navigate(new Uri(htmlFilePath).AbsoluteUri);
                }
                else
                {
                    MessageBox.Show("El archivo HTML no se encontró: " + htmlFilePath);
                }

                // Maneja los mensajes enviados desde el HTML
                webView.WebMessageReceived += WebView_WebMessageReceived;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al inicializar WebView2: " + ex.Message);
            }
        }

        private void WebView_WebMessageReceived(object sender, Microsoft.Web.WebView2.Core.CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                if (!isWebViewReady)
                {
                    MessageBox.Show("WebView aún no está listo. Intenta nuevamente.");
                    return;
                }

                var message = e.TryGetWebMessageAsString();
                if (!string.IsNullOrEmpty(message))
                {
                    if (message.Contains("username") && message.Contains("password"))
                    {
                        var pairs = message.Split('&');
                        var username = pairs[0].Split('=')[1];
                        var password = pairs[1].Split('=')[1];

                        // Llama al método de inicio de sesión
                        IniciarSesion(username, password);
                    }
                    else
                    {
                        MessageBox.Show("Formato de mensaje no válido: " + message);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al procesar el mensaje: " + ex.Message + "\nMensaje: " + e.TryGetWebMessageAsString());
            }
        }

        private async void IniciarSesion(string username, string password)
        {
            try
            {
                if (username == "admin" && password == "admin")
                {
                    // Redirige al HTML de inicio si las credenciales son correctas
                    string appDirectory = AppDomain.CurrentDomain.BaseDirectory;
                    string htmlFilePath = Path.Combine(appDirectory, "html", "inicio.html");

                    if (File.Exists(htmlFilePath))
                    {
                        if (isWebViewReady)
                        {
                            webView.CoreWebView2.Navigate(new Uri(htmlFilePath).AbsoluteUri);
                        }
                    }
                    else
                    {
                        MessageBox.Show("El archivo HTML de inicio no se encontró.");
                    }
                }
                else
                {
                    // Notifica al HTML que las credenciales son incorrectas
                    webView.CoreWebView2.ExecuteScriptAsync("document.body.innerHTML += '<p class=\"text-red-500\">Credenciales incorrectas</p>';");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al iniciar sesión: " + ex.Message);
            }
        }
    }
}
