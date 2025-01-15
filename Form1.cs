using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
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
                await webView.EnsureCoreWebView2Async();
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
                MessageBox.Show("Error al procesar el mensaje: " + ex.Message);
            }
        }

        private async void IniciarSesion(string username, string password)
        {
            try
            {
                using (var client = new HttpClient())
                {
                    client.BaseAddress = new Uri("http://10.10.13.154:8080");

                    // Llama a la API para obtener los usuarios
                    var response = await client.GetAsync("/users");
                    response.EnsureSuccessStatusCode();

                    var responseData = await response.Content.ReadAsStringAsync();
                    var users = JsonSerializer.Deserialize<User[]>(responseData);

                    // Verifica las credenciales
                    var user = users?.FirstOrDefault(u => u.name == username && u.password == password);
                    if (user != null)
                    {
                        // Redirige al HTML de inicio si las credenciales son correctas
                        string appDirectory = AppDomain.CurrentDomain.BaseDirectory;
                        string htmlFilePath = Path.Combine(appDirectory, "html", "inicio.html");

                        if (File.Exists(htmlFilePath))
                        {
                            webView.CoreWebView2.Navigate(new Uri(htmlFilePath).AbsoluteUri);

                            // Inyecta el nombre del usuario al HTML después de cargar la página
                            webView.CoreWebView2.NavigationCompleted += async (s, e) =>
                            {
                                await webView.CoreWebView2.ExecuteScriptAsync($"document.getElementById('username').innerText = '{user.name}';");
                            };
                        }
                        else
                        {
                            MessageBox.Show("El archivo HTML de inicio no se encontró.");
                        }
                    }
                    else
                    {
                        // Muestra un mensaje de error en la página HTML
                        await webView.CoreWebView2.ExecuteScriptAsync("document.body.innerHTML += '<p>Error! Credenciales incorrectas.</p>';");
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al iniciar sesión: " + ex.Message);
            }
        }


        public class User
        {
            public int id { get; set; }
            public string name { get; set; }
            public string password { get; set; }
            public bool admin { get; set; }
        }
    }
}
